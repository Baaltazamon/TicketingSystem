using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Auth;

namespace SupportPilot.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        await db.Database.MigrateAsync();

        await SeedRolesAsync(db);
        await SeedSlaPoliciesAsync(db);
        await SeedCategoriesAsync(db);
        await SeedKnowledgeBaseAsync(db);
        await SeedAdminAsync(db, scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value);
        await SeedDemoTicketsAsync(db);
    }

    private static async Task SeedRolesAsync(SupportPilotDbContext db)
    {
        foreach (var roleName in new[] { "Admin", "Agent", "Customer" })
        {
            if (!await db.Roles.AnyAsync(x => x.Name == roleName))
            {
                db.Roles.Add(new Role { Name = roleName });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSlaPoliciesAsync(SupportPilotDbContext db)
    {
        var policies = new[]
        {
            new SlaPolicy { Name = "Low", Priority = TicketPriority.Low, FirstResponseMinutes = 2880, ResolutionMinutes = 10080 },
            new SlaPolicy { Name = "Normal", Priority = TicketPriority.Normal, FirstResponseMinutes = 1440, ResolutionMinutes = 4320 },
            new SlaPolicy { Name = "High", Priority = TicketPriority.High, FirstResponseMinutes = 120, ResolutionMinutes = 1440 },
            new SlaPolicy { Name = "Critical", Priority = TicketPriority.Critical, FirstResponseMinutes = 30, ResolutionMinutes = 240 }
        };

        foreach (var policy in policies)
        {
            if (!await db.SlaPolicies.AnyAsync(x => x.Priority == policy.Priority))
            {
                db.SlaPolicies.Add(policy);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(SupportPilotDbContext db)
    {
        var categories = new[]
        {
            ("Access", "Доступы, учетные записи и права"),
            ("Billing", "Оплаты, счета и документы"),
            ("Bug", "Ошибки и нестабильная работа продукта"),
            ("Question", "Общие вопросы по продукту")
        };

        foreach (var (name, description) in categories)
        {
            if (!await db.TicketCategories.AnyAsync(x => x.Name == name))
            {
                db.TicketCategories.Add(new TicketCategory { Name = name, Description = description });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedKnowledgeBaseAsync(SupportPilotDbContext db)
    {
        if (await db.KnowledgeBaseCategories.AnyAsync())
        {
            return;
        }

        var category = new KnowledgeBaseCategory
        {
            Name = "FAQ",
            Description = "Частые вопросы пользователей"
        };
        category.Articles.Add(new KnowledgeBaseArticle
        {
            Title = "Как создать обращение",
            Slug = "how-to-create-ticket",
            Body = "Откройте раздел обращений, выберите категорию и опишите проблему максимально конкретно.",
            IsPublished = true
        });

        db.KnowledgeBaseCategories.Add(category);
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(SupportPilotDbContext db, JwtOptions options)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var admin = new User
        {
            Email = options.SeedAdminEmail,
            DisplayName = "SupportPilot Admin"
        };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, options.SeedAdminPassword);

        var adminRole = await db.Roles.SingleAsync(x => x.Name == "Admin");
        admin.UserRoles.Add(new UserRole { User = admin, Role = adminRole });

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoTicketsAsync(SupportPilotDbContext db)
    {
        if (await db.Tickets.AnyAsync(x => x.Number.StartsWith("SP-DEMO-")))
        {
            return;
        }

        var admin = await db.Users.OrderBy(x => x.Email).FirstOrDefaultAsync();
        if (admin is null)
        {
            return;
        }

        var categories = await db.TicketCategories.ToDictionaryAsync(x => x.Name);
        if (categories.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var access = GetCategory(categories, "Access");
        var billing = GetCategory(categories, "Billing");
        var bug = GetCategory(categories, "Bug");
        var question = GetCategory(categories, "Question");

        var tickets = new[]
        {
            CreateDemoTicket(
                "SP-DEMO-0001",
                "Checkout API outage for enterprise tenant",
                "Payment callbacks are failing for the largest tenant and require immediate investigation.",
                TicketStatus.InProgress,
                TicketPriority.Critical,
                bug,
                admin,
                admin.Id,
                now.AddHours(-5),
                now.AddMinutes(-8),
                now.AddHours(-4),
                now.AddHours(-1),
                true,
                true),
            CreateDemoTicket(
                "SP-DEMO-0002",
                "SSO login fails after certificate rotation",
                "Users from the finance workspace cannot complete SSO after the IdP certificate update.",
                TicketStatus.InProgress,
                TicketPriority.High,
                access,
                admin,
                admin.Id,
                now.AddHours(-3),
                now.AddMinutes(-35),
                now.AddMinutes(35),
                now.AddHours(20),
                false,
                false),
            CreateDemoTicket(
                "SP-DEMO-0003",
                "New teammate needs workspace access",
                "A new support coordinator needs access to the billing and reporting areas.",
                TicketStatus.New,
                TicketPriority.Normal,
                access,
                admin,
                null,
                now.AddHours(-2),
                now.AddHours(-1),
                now.AddHours(22),
                now.AddDays(3),
                false,
                false),
            CreateDemoTicket(
                "SP-DEMO-0004",
                "Question about invoice export format",
                "Customer needs CSV columns documented before the monthly finance export.",
                TicketStatus.WaitingForCustomer,
                TicketPriority.Normal,
                billing,
                admin,
                admin.Id,
                now.AddHours(-8),
                now.AddHours(-4),
                now.AddHours(12),
                now.AddDays(2),
                false,
                false,
                now.AddHours(-6)),
            CreateDemoTicket(
                "SP-DEMO-0005",
                "Clarify attachment size limit",
                "Support needs the latest upload limit confirmed for a customer sending diagnostic bundles.",
                TicketStatus.InProgress,
                TicketPriority.Low,
                question,
                admin,
                admin.Id,
                now.AddDays(-1),
                now.AddMinutes(-12),
                now.AddDays(1),
                now.AddDays(6),
                false,
                false,
                now.AddHours(-2))
        };

        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync();
    }

    private static TicketCategory GetCategory(IReadOnlyDictionary<string, TicketCategory> categories, string name) =>
        categories.TryGetValue(name, out var category) ? category : categories.Values.First();

    private static Ticket CreateDemoTicket(
        string number,
        string title,
        string description,
        TicketStatus status,
        TicketPriority priority,
        TicketCategory category,
        User createdBy,
        Guid? assignedToId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset firstResponseDueAt,
        DateTimeOffset resolutionDueAt,
        bool firstResponseBreached,
        bool resolutionBreached,
        DateTimeOffset? firstResponseAt = null)
    {
        var ticket = new Ticket
        {
            Number = number,
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            CategoryId = category.Id,
            CreatedById = createdBy.Id,
            AssignedToId = assignedToId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            FirstResponseDueAt = firstResponseDueAt,
            ResolutionDueAt = resolutionDueAt,
            FirstResponseAt = firstResponseAt,
            FirstResponseBreached = firstResponseBreached,
            ResolutionBreached = resolutionBreached
        };

        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            FromStatus = null,
            ToStatus = TicketStatus.New,
            ChangedById = createdBy.Id,
            Reason = "Demo seed ticket created.",
            CreatedAt = createdAt
        });

        if (status != TicketStatus.New)
        {
            ticket.StatusHistory.Add(new TicketStatusHistory
            {
                FromStatus = TicketStatus.New,
                ToStatus = status,
                ChangedById = createdBy.Id,
                Reason = "Demo seed workflow movement.",
                CreatedAt = updatedAt
            });
        }

        return ticket;
    }
}
