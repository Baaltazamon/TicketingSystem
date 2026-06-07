using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Auth;
using SupportPilot.Application.Common;
using SupportPilot.Application.Tickets;
using SupportPilot.Api.Auth;
using SupportPilot.Contracts;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Api.Endpoints;

/// <summary>
/// Defines the HTTP endpoint groups exposed by the SupportPilot API.
/// </summary>
public static class EndpointMappingExtensions
{
    /// <summary>
    /// Maps authentication endpoints for registration, login and the current user profile.
    /// </summary>
    /// <param name="app">The web application to extend.</param>
    /// <returns>The same web application instance so endpoint mapping can be chained.</returns>
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            AuthUseCases auth,
            CancellationToken cancellationToken) =>
        {
            var result = await auth.RegisterAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            AuthUseCases auth,
            CancellationToken cancellationToken) =>
        {
            var result = await auth.LoginAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        });

        group.MapGet("/me", async (
            CurrentUser currentUser,
            AuthUseCases auth,
            CancellationToken cancellationToken) =>
        {
            var result = await auth.GetCurrentUserAsync(currentUser.Id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        }).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Maps ticket endpoints for listing, creation, details, workflow changes, comments and attachments.
    /// </summary>
    /// <param name="app">The web application to extend.</param>
    /// <returns>The same web application instance so endpoint mapping can be chained.</returns>
    public static WebApplication MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets").RequireAuthorization();

        group.MapGet("/categories", async (SupportPilotDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.TicketCategories
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken)));

        group.MapGet("/", async (
            [AsParameters] TicketQuery query,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.ListAsync(query, currentUser.ToTicketActor(), cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/", async (
            CreateTicketRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.CreateAsync(request, currentUser.ToTicketActor(), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/tickets/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.GetDetailsAsync(id, currentUser.ToTicketActor(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            UpdateTicketStatusRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.ChangeStatusAsync(id, request, currentUser.ToTicketActor(), cancellationToken);
            return ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}/assignee", async (
            Guid id,
            AssignTicketRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.AssignAsync(id, request, currentUser.ToTicketActor(), cancellationToken);
            return ToHttpResult(result);
        }).RequireAuthorization("SupportStaff");

        group.MapPost("/{id:guid}/comments", async (
            Guid id,
            CreateCommentRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.AddCommentAsync(id, request, currentUser.ToTicketActor(), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/tickets/{id}/comments", new { id })
                : ToHttpResult(result);
        });

        group.MapPost("/{id:guid}/attachments", async (
            Guid id,
            HttpRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Ожидается multipart/form-data с полем file." });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Файл не передан." });
            }

            await using var fileStream = file.OpenReadStream();
            var result = await tickets.UploadAttachmentAsync(
                id,
                file.FileName,
                file.ContentType,
                fileStream,
                currentUser.ToTicketActor(),
                cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/tickets/{id}/attachments/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        group.MapGet("/{ticketId:guid}/attachments/{attachmentId:guid}", async (
            Guid ticketId,
            Guid attachmentId,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IFileStorage storage,
            CancellationToken cancellationToken) =>
        {
            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!CanReadTicket(currentUser, ticket))
            {
                return Results.Forbid();
            }

            var attachment = await db.TicketAttachments.SingleOrDefaultAsync(
                x => x.Id == attachmentId && x.TicketId == ticketId,
                cancellationToken);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            var download = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
            return download is null
                ? Results.NotFound(new { message = "Файл отсутствует в хранилище." })
                : Results.File(download.Content, attachment.ContentType, attachment.FileName);
        });

        group.MapDelete("/{ticketId:guid}/attachments/{attachmentId:guid}", async (
            Guid ticketId,
            Guid attachmentId,
            CurrentUser currentUser,
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.DeleteAttachmentAsync(ticketId, attachmentId, currentUser.ToTicketActor(), cancellationToken);
            return ToHttpResult(result);
        });

        return app;
    }

    /// <summary>
    /// Maps administrative endpoints for users, ticket categories, SLA policies and audit log access.
    /// </summary>
    /// <param name="app">The web application to extend.</param>
    /// <returns>The same web application instance so endpoint mapping can be chained.</returns>
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization("AdminOnly");

        group.MapGet("/users", async (SupportPilotDbContext db) =>
        {
            var users = await db.Users
                .AsNoTracking()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .OrderBy(x => x.Email)
                .Select(x => ToProfile(x))
                .ToListAsync();

            return Results.Ok(users);
        });

        group.MapGet("/categories", async (SupportPilotDbContext db) =>
            Results.Ok(await db.TicketCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync()));

        group.MapPost("/categories", async (UpsertCategoryRequest request, SupportPilotDbContext db) =>
        {
            var category = new TicketCategory
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                IsActive = request.IsActive
            };
            db.TicketCategories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/categories/{category.Id}", category);
        });

        group.MapPut("/categories/{id:guid}", async (Guid id, UpsertCategoryRequest request, SupportPilotDbContext db) =>
        {
            var category = await db.TicketCategories.SingleOrDefaultAsync(x => x.Id == id);
            if (category is null)
            {
                return Results.NotFound();
            }

            category.Name = request.Name.Trim();
            category.Description = request.Description;
            category.IsActive = request.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(category);
        });

        group.MapGet("/sla-policies", async (SupportPilotDbContext db) =>
            Results.Ok(await db.SlaPolicies.AsNoTracking().OrderByDescending(x => x.Priority).ToListAsync()));

        group.MapPut("/sla-policies/{id:guid}", async (Guid id, UpsertSlaPolicyRequest request, SupportPilotDbContext db) =>
        {
            var policy = await db.SlaPolicies.SingleOrDefaultAsync(x => x.Id == id);
            if (policy is null)
            {
                return Results.NotFound();
            }

            policy.Name = request.Name.Trim();
            policy.Priority = request.Priority;
            policy.FirstResponseMinutes = request.FirstResponseMinutes;
            policy.ResolutionMinutes = request.ResolutionMinutes;
            policy.IsActive = request.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(policy);
        });

        group.MapGet("/audit", async (SupportPilotDbContext db) =>
        {
            var result = await db.AuditLogs
                .FromSqlRaw("""
                    SELECT *
                    FROM "AuditLogs"
                    ORDER BY "CreatedAt" DESC
                    LIMIT 200
                    """)
                .AsNoTracking()
                .Include(x => x.Actor)
                .Select(x => new
                {
                    x.Id,
                    x.Action,
                    x.EntityName,
                    x.EntityId,
                    x.Details,
                    actor = x.Actor == null ? null : x.Actor.DisplayName,
                    x.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(result);
        });

        return app;
    }

    /// <summary>
    /// Maps knowledge base endpoints for public article browsing and support-staff article management.
    /// </summary>
    /// <param name="app">The web application to extend.</param>
    /// <returns>The same web application instance so endpoint mapping can be chained.</returns>
    public static WebApplication MapKnowledgeBaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/kb").WithTags("KnowledgeBase");

        group.MapGet("/categories", async (SupportPilotDbContext db) =>
            Results.Ok(await db.KnowledgeBaseCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync()));

        group.MapGet("/articles", async ([FromQuery] string? search, SupportPilotDbContext db) =>
        {
            var query = db.KnowledgeBaseArticles
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.IsPublished)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x => x.Title.Contains(search) || x.Body.Contains(search));
            }

            var articles = await query
                .OrderBy(x => x.Title)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Slug,
                    category = x.Category.Name,
                    x.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(articles);
        });

        group.MapGet("/articles/{slug}", async (string slug, SupportPilotDbContext db) =>
        {
            var article = await db.KnowledgeBaseArticles
                .AsNoTracking()
                .Include(x => x.Category)
                .SingleOrDefaultAsync(x => x.Slug == slug && x.IsPublished);

            return article is null ? Results.NotFound() : Results.Ok(article);
        });

        var adminGroup = group.MapGroup("/admin").RequireAuthorization("SupportStaff");

        adminGroup.MapPost("/categories", async (UpsertKnowledgeBaseCategoryRequest request, SupportPilotDbContext db) =>
        {
            var category = new KnowledgeBaseCategory { Name = request.Name.Trim(), Description = request.Description };
            db.KnowledgeBaseCategories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/kb/categories/{category.Id}", category);
        });

        adminGroup.MapPost("/articles", async (UpsertKnowledgeBaseArticleRequest request, SupportPilotDbContext db) =>
        {
            var article = new KnowledgeBaseArticle
            {
                CategoryId = request.CategoryId,
                Title = request.Title.Trim(),
                Slug = request.Slug.Trim().ToLowerInvariant(),
                Body = request.Body,
                IsPublished = request.IsPublished
            };
            db.KnowledgeBaseArticles.Add(article);
            await db.SaveChangesAsync();
            return Results.Created($"/api/kb/articles/{article.Slug}", article);
        });

        adminGroup.MapPut("/articles/{id:guid}", async (Guid id, UpsertKnowledgeBaseArticleRequest request, SupportPilotDbContext db) =>
        {
            var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(x => x.Id == id);
            if (article is null)
            {
                return Results.NotFound();
            }

            article.CategoryId = request.CategoryId;
            article.Title = request.Title.Trim();
            article.Slug = request.Slug.Trim().ToLowerInvariant();
            article.Body = request.Body;
            article.IsPublished = request.IsPublished;
            article.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(article);
        });

        return app;
    }

    /// <summary>
    /// Maps reporting endpoints for support staff operational dashboards.
    /// </summary>
    /// <param name="app">The web application to extend.</param>
    /// <returns>The same web application instance so endpoint mapping can be chained.</returns>
    public static WebApplication MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization("SupportStaff");

        group.MapGet("/overview", async (SupportPilotDbContext db) =>
        {
            var now = DateTimeOffset.UtcNow;
            var tickets = await db.Tickets.AsNoTracking().ToListAsync();
            var result = new
            {
                total = tickets.Count,
                open = tickets.Count(x => x.Status is TicketStatus.New or TicketStatus.InProgress or TicketStatus.WaitingForCustomer),
                resolved = tickets.Count(x => x.Status is TicketStatus.Resolved or TicketStatus.Closed),
                slaBreached = tickets.Count(x => x.FirstResponseBreached || x.ResolutionBreached),
                dueSoon = tickets.Count(x => x.ResolutionDueAt > now && x.ResolutionDueAt <= now.AddHours(4)),
                byStatus = tickets.GroupBy(x => x.Status).Select(x => new { status = x.Key, count = x.Count() }),
                byPriority = tickets.GroupBy(x => x.Priority).Select(x => new { priority = x.Key, count = x.Count() })
            };

            return Results.Ok(result);
        });

        return app;
    }

    /// <summary>
    /// Maps notification endpoints for reading user notifications and marking them as read.
    /// </summary>
    /// <param name="app">The web application to extend.</param>
    /// <returns>The same web application instance so endpoint mapping can be chained.</returns>
    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async (CurrentUser currentUser, SupportPilotDbContext db) =>
        {
            var notifications = await db.Notifications
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Notifications"
                    WHERE "UserId" = {currentUser.Id} OR "UserId" IS NULL
                    ORDER BY "CreatedAt" DESC
                    LIMIT 100
                    """)
                .AsNoTracking()
                .ToListAsync();

            return Results.Ok(notifications);
        });

        group.MapPost("/{id:guid}/read", async (Guid id, CurrentUser currentUser, SupportPilotDbContext db) =>
        {
            var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id);
            if (notification is null)
            {
                return Results.NotFound();
            }

            notification.IsRead = true;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static IResult ToHttpResult(ApplicationResult result)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToHttpError(result.Error, result.Message);
    }

    private static IResult ToHttpResult<T>(ApplicationResult<T> result) =>
        ToHttpError(result.Error, result.Message);

    private static IResult ToHttpError(ApplicationError error, string? message) =>
        error switch
        {
            ApplicationError.Validation => Results.BadRequest(new { message }),
            ApplicationError.NotFound => Results.NotFound(new { message }),
            ApplicationError.Unauthorized => Results.Unauthorized(),
            ApplicationError.Forbidden => Results.Forbid(),
            ApplicationError.Conflict => Results.Conflict(new { message }),
            _ => Results.BadRequest(new { message })
        };

    private static TicketActor ToTicketActor(this CurrentUser currentUser) =>
        new(currentUser.Id, currentUser.IsInRole("Admin"), currentUser.IsInRole("Admin") || currentUser.IsInRole("Agent"));

    private static bool CanReadTicket(CurrentUser currentUser, Ticket ticket) =>
        currentUser.IsInRole("Admin") || currentUser.IsInRole("Agent") || ticket.CreatedById == currentUser.Id;

    private static UserProfileResponse ToProfile(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.UserRoles.Select(x => x.Role.Name).Order().ToArray());
}
