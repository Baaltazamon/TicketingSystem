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
    public async Task ReturnsActiveTicketCategoriesForAuthenticatedUsers()
    {
        var client = await CreateAuthenticatedClientAsync("categories-customer");

        var response = await client.GetAsync("/api/tickets/categories");

        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var rows = json.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, category => Assert.True(category.GetProperty("isActive").GetBoolean()));
        Assert.Equal(
            rows.Select(category => category.GetProperty("name").GetString()).OrderBy(name => name).ToList(),
            rows.Select(category => category.GetProperty("name").GetString()).ToList());
    }

    [Fact]
    public async Task ReportsOverviewReturnsDashboardSnapshotForSupportStaff()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var ticketId = await CreateTicketAsync(admin, "Dashboard overview ticket");

        var response = await admin.GetAsync("/api/reports/overview");

        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.True(root.GetProperty("totalTickets").GetInt32() >= 1);
        Assert.True(root.GetProperty("openTickets").GetInt32() >= 1);
        Assert.True(root.GetProperty("unassignedTickets").GetInt32() >= 1);
        Assert.NotEmpty(root.GetProperty("byStatus").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("byPriority").EnumerateArray());
        Assert.Contains(
            root.GetProperty("recentTickets").EnumerateArray(),
            ticket => ticket.GetProperty("id").GetGuid() == ticketId);
    }

    [Fact]
    public async Task ReportsOverviewRequiresSupportStaff()
    {
        var customer = await CreateAuthenticatedClientAsync("dashboard-customer");

        var response = await customer.GetAsync("/api/reports/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task KnowledgeBaseAdminCanCreateDraftAndPublishArticle()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var slug = $"kb-{Guid.NewGuid():N}";

        var categoryResponse = await admin.PostAsJsonAsync("/api/kb/admin/categories", new
        {
            name = $"KB {Guid.NewGuid():N}",
            description = "Integration test category"
        });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        using var categoryJson = await JsonDocument.ParseAsync(await categoryResponse.Content.ReadAsStreamAsync());
        var categoryId = categoryJson.RootElement.GetProperty("id").GetGuid();

        var draftResponse = await admin.PostAsJsonAsync("/api/kb/admin/articles", new
        {
            categoryId,
            title = "Draft article",
            slug,
            body = "Draft body",
            isPublished = false
        });
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        using var draftJson = await JsonDocument.ParseAsync(await draftResponse.Content.ReadAsStreamAsync());
        var articleId = draftJson.RootElement.GetProperty("id").GetGuid();
        Assert.False(draftJson.RootElement.GetProperty("isPublished").GetBoolean());

        var hiddenDraft = await factory.CreateClient().GetAsync($"/api/kb/articles/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDraft.StatusCode);

        var adminDetails = await admin.GetAsync($"/api/kb/admin/articles/{articleId}");
        adminDetails.EnsureSuccessStatusCode();
        using var adminDetailsJson = await JsonDocument.ParseAsync(await adminDetails.Content.ReadAsStreamAsync());
        Assert.Equal("Draft body", adminDetailsJson.RootElement.GetProperty("body").GetString());

        var publishResponse = await admin.PutAsJsonAsync($"/api/kb/admin/articles/{articleId}", new
        {
            categoryId,
            title = "Published article",
            slug,
            body = "Published body",
            isPublished = true
        });
        publishResponse.EnsureSuccessStatusCode();

        var publicArticle = await factory.CreateClient().GetAsync($"/api/kb/articles/{slug}");
        publicArticle.EnsureSuccessStatusCode();
        using var publicJson = await JsonDocument.ParseAsync(await publicArticle.Content.ReadAsStreamAsync());
        Assert.Equal("Published article", publicJson.RootElement.GetProperty("title").GetString());
        Assert.True(publicJson.RootElement.GetProperty("isPublished").GetBoolean());
    }

    [Fact]
    public async Task KnowledgeBaseAdminRequiresSupportStaff()
    {
        var customer = await CreateAuthenticatedClientAsync("kb-customer");

        var response = await customer.GetAsync("/api/kb/admin/articles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanUpdateUserRolesAndActiveState()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var userId = await CreateUserAsync("managed-user", "Customer");

        var update = await admin.PutAsJsonAsync($"/api/admin/users/{userId}", new
        {
            displayName = "Managed Agent",
            isActive = false,
            roles = new[] { "Agent" }
        });

        update.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await update.Content.ReadAsStreamAsync());
        Assert.Equal("Managed Agent", json.RootElement.GetProperty("displayName").GetString());
        Assert.False(json.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Contains(json.RootElement.GetProperty("roles").EnumerateArray(), role => role.GetString() == "Agent");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == userId);
        Assert.False(user.IsActive);
        Assert.Equal("Managed Agent", user.DisplayName);
        Assert.Contains(user.UserRoles, role => role.Role.Name == "Agent");
        Assert.DoesNotContain(user.UserRoles, role => role.Role.Name == "Customer");
    }

    [Fact]
    public async Task AdminCannotDisableLastActiveAdministrator()
    {
        var admin = await CreateAuthenticatedClientAsync();
        Guid adminId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
            adminId = await db.Users
                .Where(x => x.Email == "admin@supportpilot.local")
                .Select(x => x.Id)
                .SingleAsync();
        }

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{adminId}", new
        {
            displayName = "SupportPilot Admin",
            isActive = false,
            roles = new[] { "Admin" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanManageTicketCategoriesAndSlaPolicies()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var categoryName = $"Managed {Guid.NewGuid():N}";

        var categoryCreate = await admin.PostAsJsonAsync("/api/admin/categories", new
        {
            name = categoryName,
            description = "Created from admin integration test",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, categoryCreate.StatusCode);
        using var categoryJson = await JsonDocument.ParseAsync(await categoryCreate.Content.ReadAsStreamAsync());
        var categoryId = categoryJson.RootElement.GetProperty("id").GetGuid();

        var categoryUpdate = await admin.PutAsJsonAsync($"/api/admin/categories/{categoryId}", new
        {
            name = categoryName,
            description = "Updated from admin integration test",
            isActive = false
        });
        categoryUpdate.EnsureSuccessStatusCode();
        using var updatedCategoryJson = await JsonDocument.ParseAsync(await categoryUpdate.Content.ReadAsStreamAsync());
        Assert.False(updatedCategoryJson.RootElement.GetProperty("isActive").GetBoolean());

        Guid policyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
            policyId = await db.SlaPolicies
                .Where(x => x.Priority == TicketPriority.High)
                .Select(x => x.Id)
                .SingleAsync();
        }

        var slaUpdate = await admin.PutAsJsonAsync($"/api/admin/sla-policies/{policyId}", new
        {
            name = "High Managed",
            priority = TicketPriority.High,
            firstResponseMinutes = 90,
            resolutionMinutes = 720,
            isActive = true
        });
        slaUpdate.EnsureSuccessStatusCode();
        using var slaJson = await JsonDocument.ParseAsync(await slaUpdate.Content.ReadAsStreamAsync());
        Assert.Equal(90, slaJson.RootElement.GetProperty("firstResponseMinutes").GetInt32());
        Assert.Equal(720, slaJson.RootElement.GetProperty("resolutionMinutes").GetInt32());
    }

    [Fact]
    public async Task KnowledgeBaseAdminChangesInvalidatePublicCache()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var publicClient = factory.CreateClient();
        var categoryName = $"Cached KB {Guid.NewGuid():N}";

        var firstPublicRead = await publicClient.GetAsync("/api/kb/categories");
        firstPublicRead.EnsureSuccessStatusCode();

        var createCategory = await admin.PostAsJsonAsync("/api/kb/admin/categories", new
        {
            name = categoryName,
            description = "Cache invalidation category"
        });
        Assert.Equal(HttpStatusCode.Created, createCategory.StatusCode);

        var secondPublicRead = await publicClient.GetAsync("/api/kb/categories");
        secondPublicRead.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await secondPublicRead.Content.ReadAsStreamAsync());

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            category => category.GetProperty("name").GetString() == categoryName);
    }

    [Fact]
    public async Task TicketChangesInvalidateReportsCache()
    {
        var admin = await CreateAuthenticatedClientAsync();

        var beforeCreate = await admin.GetAsync("/api/reports/overview");
        beforeCreate.EnsureSuccessStatusCode();
        using var beforeJson = await JsonDocument.ParseAsync(await beforeCreate.Content.ReadAsStreamAsync());
        var beforeTotal = beforeJson.RootElement.GetProperty("totalTickets").GetInt32();

        await CreateTicketAsync(admin, "Report cache invalidation ticket");

        var afterCreate = await admin.GetAsync("/api/reports/overview");
        afterCreate.EnsureSuccessStatusCode();
        using var afterJson = await JsonDocument.ParseAsync(await afterCreate.Content.ReadAsStreamAsync());
        var afterTotal = afterJson.RootElement.GetProperty("totalTickets").GetInt32();

        Assert.Equal(beforeTotal + 1, afterTotal);
    }

    [Fact]
    public async Task AuditEndpointOrdersAndLimitsRowsInSqliteMode()
    {
        var client = await CreateAuthenticatedClientAsync();
        var marker = $"audit-order-{Guid.NewGuid():N}";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
            var baseTime = DateTimeOffset.UtcNow.AddYears(10);
            for (var i = 0; i < 205; i++)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Action = AuditAction.Created,
                    EntityName = nameof(Ticket),
                    EntityId = Guid.NewGuid().ToString(),
                    Details = $"{marker}-{i:000}",
                    CreatedAt = baseTime.AddMinutes(i)
                });
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/admin/audit");
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var rows = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(200, rows.Count);
        Assert.Equal($"{marker}-204", rows[0].GetProperty("details").GetString());
    }

    [Fact]
    public async Task NotificationsEndpointOrdersAndLimitsRowsInSqliteMode()
    {
        var client = await CreateAuthenticatedClientAsync();
        var marker = $"notification-order-{Guid.NewGuid():N}";
        Guid adminId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
            adminId = await db.Users
                .Where(x => x.Email == "admin@supportpilot.local")
                .Select(x => x.Id)
                .SingleAsync();

            var baseTime = DateTimeOffset.UtcNow.AddYears(10);
            for (var i = 0; i < 105; i++)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = adminId,
                    Type = NotificationType.TicketUpdated,
                    Message = $"{marker}-{i:000}",
                    CreatedAt = baseTime.AddMinutes(i)
                });
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/notifications");
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var rows = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(100, rows.Count);
        Assert.Equal($"{marker}-104", rows[0].GetProperty("message").GetString());
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

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
            Assert.True(await db.Notifications.AnyAsync(x =>
                x.TicketId == ticketId &&
                x.UserId == agentId &&
                x.Type == NotificationType.TicketAssigned));
        }

        var mine = await agent.GetAsync("/api/tickets?mine=true");
        mine.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await mine.Content.ReadAsStreamAsync());
        Assert.Contains(json.RootElement.EnumerateArray(), ticket => ticket.GetProperty("id").GetGuid() == ticketId);
    }

    [Fact]
    public async Task AssigneeLookupRequiresSupportStaffAndReturnsOnlyActiveSupportUsers()
    {
        var agentId = await CreateUserAsync("lookup-agent", "Agent");
        var inactiveAgentId = await CreateUserAsync("lookup-inactive-agent", "Agent");
        var customerId = await CreateUserAsync("lookup-customer", "Customer");
        var agent = await LoginAsync($"lookup-agent-{agentId:N}@supportpilot.local", "Password123!");
        var customer = await LoginAsync($"lookup-customer-{customerId:N}@supportpilot.local", "Password123!");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
            var inactiveAgent = await db.Users.SingleAsync(x => x.Id == inactiveAgentId);
            inactiveAgent.IsActive = false;
            await db.SaveChangesAsync();
        }

        var forbidden = await customer.GetAsync("/api/tickets/assignees");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var response = await agent.GetAsync("/api/tickets/assignees");
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var users = json.RootElement.EnumerateArray().ToArray();

        Assert.Contains(users, user => user.GetProperty("id").GetGuid() == agentId);
        Assert.Contains(users, user => user.GetProperty("email").GetString() == "admin@supportpilot.local");
        Assert.DoesNotContain(users, user => user.GetProperty("id").GetGuid() == customerId);
        Assert.DoesNotContain(users, user => user.GetProperty("id").GetGuid() == inactiveAgentId);
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
