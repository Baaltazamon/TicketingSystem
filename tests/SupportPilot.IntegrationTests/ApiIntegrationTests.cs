using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    private async Task<Guid> GetFirstCategoryIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        return await db.TicketCategories
            .OrderBy(x => x.Name)
            .Select(x => x.Id)
            .FirstAsync();
    }

    private async Task<Guid> CreateTicketAsync(HttpClient client)
    {
        var categoryId = await GetFirstCategoryIdAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Attachment ticket",
            description = "Created for attachment test",
            categoryId,
            priority = TicketPriority.Normal
        });
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
