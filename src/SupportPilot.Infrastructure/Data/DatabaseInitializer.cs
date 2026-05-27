using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportPilot.Domain;
using SupportPilot.Domain.Domain;
using SupportPilot.Infrastructure.Auth;

namespace SupportPilot.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedRolesAsync(db);
        await SeedSlaPoliciesAsync(db);
        await SeedCategoriesAsync(db);
        await SeedKnowledgeBaseAsync(db);
        await SeedAdminAsync(db, scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value);
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
}
