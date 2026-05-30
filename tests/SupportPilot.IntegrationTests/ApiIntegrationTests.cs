using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;
using SupportPilot.Infrastructure.Services;

namespace SupportPilot.IntegrationTests;

public sealed class ApiIntegrationTests(SupportPilotApiFactory factory) : IClassFixture<SupportPilotApiFactory>
{
    [Fact]
    public async Task AppliesMigrationsOnStartup()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();

        Assert.Contains(appliedMigrations, migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
        Assert.True(await db.Roles.AnyAsync(x => x.Name == "Admin"));
    }

    [Fact]
    public async Task CreatesTicketThroughApi()
    {
        var client = await CreateAuthenticatedClientAsync();
        var categoryId = await GetFirstCategoryIdAsync();

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Integration ticket",
            description = "Created from integration test",
            categoryId,
            priority = TicketPriority.Normal
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.StartsWith("SP-", json.RootElement.GetProperty("number").GetString());
    }

    [Fact]
    public async Task RegistersLogsInAndReturnsCurrentUser()
    {
        var client = factory.CreateClient();
        var email = $"auth-{Guid.NewGuid():N}@supportpilot.local";

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "Auth Test User",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var registerJson = await JsonDocument.ParseAsync(await register.Content.ReadAsStreamAsync());
        Assert.Equal(email, registerJson.RootElement.GetProperty("email").GetString());
        Assert.Contains(
            registerJson.RootElement.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == "Customer");

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var token = loginJson.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meJson = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync());
        Assert.Equal(email, meJson.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task AuthReturnsConsistentErrors()
    {
        var client = factory.CreateClient();
        var email = $"duplicate-{Guid.NewGuid():N}@supportpilot.local";
        var payload = new
        {
            email,
            displayName = "Duplicate User",
            password = "Password123!"
        };

        var firstRegister = await client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.Created, firstRegister.StatusCode);

        var duplicateRegister = await client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRegister.StatusCode);

        var invalidLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);

        var anonymousMe = await factory.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);
    }

    [Fact]
    public async Task UploadsDownloadsAndDeletesAttachment()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ticketId = await CreateTicketAsync(client);
        var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("attachment payload"))
            {
                Headers = { ContentType = MediaTypeHeaderValue.Parse("text/plain") }
            },
            "file",
            "payload.txt");

        var upload = await client.PostAsync($"/api/tickets/{ticketId}/attachments", content);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        using var uploadJson = await JsonDocument.ParseAsync(await upload.Content.ReadAsStreamAsync());
        var attachmentId = uploadJson.RootElement.GetProperty("id").GetGuid();

        var download = await client.GetAsync($"/api/tickets/{ticketId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment payload", await download.Content.ReadAsStringAsync());

        var delete = await client.DeleteAsync($"/api/tickets/{ticketId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var downloadAfterDelete = await client.GetAsync($"/api/tickets/{ticketId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, downloadAfterDelete.StatusCode);
    }

    [Fact]
    public async Task SlaBreachProcessorMarksOverdueTicket()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        var admin = await db.Users.SingleAsync(x => x.Email == "admin@supportpilot.local");
        var category = await db.TicketCategories.FirstAsync();
        var ticket = new Ticket
        {
            Number = "SP-TEST-SLA",
            Title = "Overdue ticket",
            Description = "SLA breach test",
            CategoryId = category.Id,
            CreatedById = admin.Id,
            Priority = TicketPriority.Critical,
            FirstResponseDueAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ResolutionDueAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<SlaBreachProcessor>();
        var breached = await processor.CheckSlaAsync(CancellationToken.None);

        var updated = await db.Tickets.SingleAsync(x => x.Id == ticket.Id);
        Assert.Equal(1, breached);
        Assert.True(updated.FirstResponseBreached);
        Assert.True(updated.ResolutionBreached);
        Assert.True(await db.Notifications.AnyAsync(x => x.TicketId == ticket.Id && x.Type == NotificationType.SlaBreached));
    }

    [Fact]
    public async Task RejectsInvalidStatusTransition()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ticketId = await CreateTicketAsync(client, "Invalid transition ticket");

        var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/status", new
        {
            status = TicketStatus.Closed,
            reason = "Skip workflow"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        var ticket = await db.Tickets.SingleAsync(x => x.Id == ticketId);
        Assert.Equal(TicketStatus.New, ticket.Status);
    }

    [Fact]
    public async Task CustomerCannotReadAnotherCustomersTicket()
    {
        var owner = await CreateAuthenticatedClientAsync("owner");
        var other = await CreateAuthenticatedClientAsync("other");
        var ticketId = await CreateTicketAsync(owner, "Private customer ticket");

        var response = await other.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InternalNotesAreVisibleOnlyToSupportStaff()
    {
        var customer = await CreateAuthenticatedClientAsync("note-owner");
        var agent = await CreateAuthenticatedClientAsync("note-agent", "Agent");
        var ticketId = await CreateTicketAsync(customer, "Internal note ticket");

        var addNote = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/comments", new
        {
            body = "internal investigation",
            isInternal = true
        });
        Assert.Equal(HttpStatusCode.Created, addNote.StatusCode);

        var customerDetails = await customer.GetAsync($"/api/tickets/{ticketId}");
        customerDetails.EnsureSuccessStatusCode();
        using var customerJson = await JsonDocument.ParseAsync(await customerDetails.Content.ReadAsStreamAsync());
        Assert.Empty(customerJson.RootElement.GetProperty("comments").EnumerateArray());

        var agentDetails = await agent.GetAsync($"/api/tickets/{ticketId}");
        agentDetails.EnsureSuccessStatusCode();
        using var agentJson = await JsonDocument.ParseAsync(await agentDetails.Content.ReadAsStreamAsync());
        Assert.Contains(
            agentJson.RootElement.GetProperty("comments").EnumerateArray(),
            comment => comment.GetProperty("isInternal").GetBoolean() &&
                       comment.GetProperty("body").GetString() == "internal investigation");
    }

    [Fact]
    public async Task AssignmentRequiresSupportStaffAndMineFilterReturnsAssignedTickets()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var customerId = await CreateUserAsync("assignee-customer", "Customer");
        var agentId = await CreateUserAsync("assignee-agent", "Agent");
        var agent = await LoginAsync($"assignee-agent-{agentId:N}@supportpilot.local", "Password123!");
        var ticketId = await CreateTicketAsync(admin, "Assignment workflow ticket");

        var invalidAssignment = await admin.PatchAsJsonAsync($"/api/tickets/{ticketId}/assignee", new
        {
            assignedToId = customerId
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAssignment.StatusCode);

        var validAssignment = await admin.PatchAsJsonAsync($"/api/tickets/{ticketId}/assignee", new
        {
            assignedToId = agentId
        });
        Assert.Equal(HttpStatusCode.NoContent, validAssignment.StatusCode);

        var mine = await agent.GetAsync("/api/tickets?mine=true");
        mine.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await mine.Content.ReadAsStreamAsync());
        Assert.Contains(json.RootElement.EnumerateArray(), ticket => ticket.GetProperty("id").GetGuid() == ticketId);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@supportpilot.local",
            password = "Admin123!"
        });
        login.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var token = json.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string userName, string role = "Customer")
    {
        var userId = await CreateUserAsync(userName, role);
        return await LoginAsync($"{userName}-{userId:N}@supportpilot.local", "Password123!");
    }

    private async Task<Guid> CreateUserAsync(string userName, string role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        var roleEntity = await db.Roles.SingleAsync(x => x.Name == role);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = $"{userName}-{userId:N}@supportpilot.local",
            DisplayName = $"{role} {userName}"
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Password123!");
        user.UserRoles.Add(new UserRole { User = user, Role = roleEntity });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        login.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var token = json.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetFirstCategoryIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        return await db.TicketCategories
            .OrderBy(x => x.Name)
            .Select(x => x.Id)
            .FirstAsync();
    }

    private async Task<Guid> CreateTicketAsync(HttpClient client, string title = "Attachment ticket")
    {
        var categoryId = await GetFirstCategoryIdAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = "Created for attachment test",
            categoryId,
            priority = TicketPriority.Normal
        });
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
