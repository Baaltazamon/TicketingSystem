using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Admin;
using SupportPilot.Application.Auth;
using SupportPilot.Application.Common;
using SupportPilot.Application.Tickets;
using SupportPilot.Api.Auth;
using SupportPilot.Contracts;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;
using SupportPilot.Infrastructure.Services;

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

        group.MapGet("/assignees", async (SupportPilotDbContext db, CancellationToken cancellationToken) =>
        {
            var users = await db.Users
                .AsNoTracking()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Where(x => x.IsActive && x.UserRoles.Any(role => role.Role.Name == "Admin" || role.Role.Name == "Agent"))
                .OrderBy(x => x.DisplayName)
                .ThenBy(x => x.Email)
                .Select(x => ToProfile(x))
                .ToListAsync(cancellationToken);

            return Results.Ok(users);
        }).RequireAuthorization("SupportStaff");

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
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.CreateAsync(request, currentUser.ToTicketActor(), cancellationToken);
            if (result.IsSuccess)
            {
                await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);
            }

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
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.ChangeStatusAsync(id, request, currentUser.ToTicketActor(), cancellationToken);
            if (result.IsSuccess)
            {
                await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);
            }

            return ToHttpResult(result);
        });

        group.MapPatch("/{id:guid}/assignee", async (
            Guid id,
            AssignTicketRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.AssignAsync(id, request, currentUser.ToTicketActor(), cancellationToken);
            if (result.IsSuccess)
            {
                await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);
            }

            return ToHttpResult(result);
        }).RequireAuthorization("SupportStaff");

        group.MapPost("/{id:guid}/comments", async (
            Guid id,
            CreateCommentRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.AddCommentAsync(id, request, currentUser.ToTicketActor(), cancellationToken);
            if (result.IsSuccess)
            {
                await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);
            }

            return result.IsSuccess
                ? Results.Created($"/api/tickets/{id}/comments", new { id })
                : ToHttpResult(result);
        });

        group.MapPost("/{id:guid}/attachments", async (
            Guid id,
            HttpRequest request,
            CurrentUser currentUser,
            TicketUseCases tickets,
            IApplicationCache cache,
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
            if (result.IsSuccess)
            {
                await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);
            }

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
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.DeleteAttachmentAsync(ticketId, attachmentId, currentUser.ToTicketActor(), cancellationToken);
            if (result.IsSuccess)
            {
                await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);
            }

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

        group.MapGet("/users", async (AdminUseCases admin, CancellationToken cancellationToken) =>
            Results.Ok(await admin.ListUsersAsync(cancellationToken)))
            .WithRouteDocs(
                "List users for administration",
                "Returns all users with roles, active state and creation timestamp. Requires the Admin role.")
            .Produces<IReadOnlyList<AdminUserResponse>>();

        group.MapGet("/roles", async (AdminUseCases admin, CancellationToken cancellationToken) =>
            Results.Ok(await admin.ListRolesAsync(cancellationToken)))
            .WithRouteDocs(
                "List application roles",
                "Returns role names that can be assigned by administrators.")
            .Produces<IReadOnlyList<string>>();

        group.MapPut("/users/{id:guid}", async (
            Guid id,
            UpdateAdminUserRequest request,
            AdminUseCases admin,
            CancellationToken cancellationToken) =>
        {
            var result = await admin.UpdateUserAsync(id, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        })
            .WithRouteDocs(
                "Update an administrative user",
                "Updates display name, active state and assigned roles. The last active administrator is protected from deactivation or role removal.")
            .Produces<AdminUserResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/categories", async (AdminUseCases admin, CancellationToken cancellationToken) =>
            Results.Ok(await admin.ListTicketCategoriesAsync(cancellationToken)))
            .WithRouteDocs(
                "List ticket categories for administration",
                "Returns active and inactive ticket categories sorted by name.")
            .Produces<IReadOnlyList<TicketCategory>>();

        group.MapPost("/categories", async (
            UpsertCategoryRequest request,
            AdminUseCases admin,
            CancellationToken cancellationToken) =>
        {
            var result = await admin.CreateTicketCategoryAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/admin/categories/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        })
            .WithRouteDocs(
                "Create a ticket category",
                "Creates a category that can be made available for new tickets.")
            .Produces<TicketCategory>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/categories/{id:guid}", async (
            Guid id,
            UpsertCategoryRequest request,
            AdminUseCases admin,
            CancellationToken cancellationToken) =>
        {
            var result = await admin.UpdateTicketCategoryAsync(id, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        })
            .WithRouteDocs(
                "Update a ticket category",
                "Updates category name, description and active state.")
            .Produces<TicketCategory>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sla-policies", async (AdminUseCases admin, CancellationToken cancellationToken) =>
            Results.Ok(await admin.ListSlaPoliciesAsync(cancellationToken)))
            .WithRouteDocs(
                "List SLA policies",
                "Returns all configured SLA policies sorted by priority descending.")
            .Produces<IReadOnlyList<SlaPolicy>>();

        group.MapPut("/sla-policies/{id:guid}", async (
            Guid id,
            UpsertSlaPolicyRequest request,
            AdminUseCases admin,
            CancellationToken cancellationToken) =>
        {
            var result = await admin.UpdateSlaPolicyAsync(id, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        })
            .WithRouteDocs(
                "Update an SLA policy",
                "Updates SLA thresholds, priority mapping and active state, then invalidates report cache.")
            .Produces<SlaPolicy>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

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
        })
            .WithRouteDocs(
                "List recent audit events",
                "Returns the 200 most recent audit log entries with actor display names.")
            .Produces(StatusCodes.Status200OK);

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

        group.MapGet("/categories", async (
            SupportPilotDbContext db,
            IApplicationCache cache,
            IOptions<CacheOptions> cacheOptions,
            CancellationToken cancellationToken) =>
            Results.Ok(await cache.GetOrCreateAsync(
                CacheGroups.KnowledgeBase,
                "categories",
                KnowledgeBaseCacheExpiration(cacheOptions),
                token => db.KnowledgeBaseCategories
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .Select(x => new KnowledgeBaseCategoryResponse(
                        x.Id,
                        x.Name,
                        x.Description,
                        x.Articles.Count(article => article.IsPublished)))
                    .ToListAsync(token),
                cancellationToken)));

        group.MapGet("/articles", async (
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            SupportPilotDbContext db,
            IApplicationCache cache,
            IOptions<CacheOptions> cacheOptions,
            CancellationToken cancellationToken) =>
        {
            var normalizedSearch = search?.Trim();
            var cacheKey = $"articles:search={normalizedSearch?.ToLowerInvariant() ?? "all"}:category={categoryId?.ToString("N") ?? "all"}";
            var articles = await cache.GetOrCreateAsync(
                CacheGroups.KnowledgeBase,
                cacheKey,
                KnowledgeBaseCacheExpiration(cacheOptions),
                token =>
                {
                    var query = db.KnowledgeBaseArticles
                        .AsNoTracking()
                        .Include(x => x.Category)
                        .Where(x => x.IsPublished)
                        .AsQueryable();

                    if (!string.IsNullOrWhiteSpace(normalizedSearch))
                    {
                        query = query.Where(x => x.Title.Contains(normalizedSearch) || x.Body.Contains(normalizedSearch));
                    }

                    if (categoryId.HasValue)
                    {
                        query = query.Where(x => x.CategoryId == categoryId.Value);
                    }

                    return query
                        .OrderBy(x => x.Title)
                        .Select(x => new KnowledgeBaseArticleListItemResponse(
                            x.Id,
                            x.Title,
                            x.Slug,
                            x.CategoryId,
                            x.Category.Name,
                            x.IsPublished,
                            x.UpdatedAt))
                        .ToListAsync(token);
                },
                cancellationToken);

            return Results.Ok(articles);
        });

        group.MapGet("/articles/{slug}", async (
            string slug,
            SupportPilotDbContext db,
            IApplicationCache cache,
            IOptions<CacheOptions> cacheOptions,
            CancellationToken cancellationToken) =>
        {
            var normalizedSlug = slug.Trim().ToLowerInvariant();
            var article = await cache.GetOrCreateAsync(
                CacheGroups.KnowledgeBase,
                $"article:{normalizedSlug}",
                KnowledgeBaseCacheExpiration(cacheOptions),
                async token =>
                {
                    var entity = await db.KnowledgeBaseArticles
                        .AsNoTracking()
                        .Include(x => x.Category)
                        .SingleOrDefaultAsync(x => x.Slug == normalizedSlug && x.IsPublished, token);

                    return entity is null ? null : ToKnowledgeBaseArticleResponse(entity);
                },
                cancellationToken);

            return article is null
                ? Results.NotFound()
                : Results.Ok(article);
        });

        var adminGroup = group.MapGroup("/admin").RequireAuthorization("SupportStaff");

        adminGroup.MapGet("/categories", async (SupportPilotDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.KnowledgeBaseCategories
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new KnowledgeBaseCategoryResponse(
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Articles.Count))
                .ToListAsync(cancellationToken)));

        adminGroup.MapPost("/categories", async (
            UpsertKnowledgeBaseCategoryRequest request,
            SupportPilotDbContext db,
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var category = new KnowledgeBaseCategory { Name = request.Name.Trim(), Description = request.Description };
            db.KnowledgeBaseCategories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
            await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);
            return Results.Created(
                $"/api/kb/admin/categories/{category.Id}",
                new KnowledgeBaseCategoryResponse(category.Id, category.Name, category.Description, 0));
        });

        adminGroup.MapPut("/categories/{id:guid}", async (
            Guid id,
            UpsertKnowledgeBaseCategoryRequest request,
            SupportPilotDbContext db,
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var category = await db.KnowledgeBaseCategories
                .Include(x => x.Articles)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (category is null)
            {
                return Results.NotFound();
            }

            category.Name = request.Name.Trim();
            category.Description = request.Description;
            await db.SaveChangesAsync(cancellationToken);
            await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);

            return Results.Ok(new KnowledgeBaseCategoryResponse(
                category.Id,
                category.Name,
                category.Description,
                category.Articles.Count));
        });

        adminGroup.MapGet("/articles", async (
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool? published,
            SupportPilotDbContext db,
            CancellationToken cancellationToken) =>
        {
            var query = db.KnowledgeBaseArticles
                .AsNoTracking()
                .Include(x => x.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x => x.Title.Contains(search) || x.Body.Contains(search) || x.Slug.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(x => x.CategoryId == categoryId.Value);
            }

            if (published.HasValue)
            {
                query = query.Where(x => x.IsPublished == published.Value);
            }

            var articles = await query
                .OrderBy(x => x.Title)
                .Select(x => new KnowledgeBaseArticleListItemResponse(
                    x.Id,
                    x.Title,
                    x.Slug,
                    x.CategoryId,
                    x.Category.Name,
                    x.IsPublished,
                    x.UpdatedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(articles);
        });

        adminGroup.MapGet("/articles/{id:guid}", async (
            Guid id,
            SupportPilotDbContext db,
            CancellationToken cancellationToken) =>
        {
            var article = await db.KnowledgeBaseArticles
                .AsNoTracking()
                .Include(x => x.Category)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            return article is null
                ? Results.NotFound()
                : Results.Ok(ToKnowledgeBaseArticleResponse(article));
        });

        adminGroup.MapPost("/articles", async (
            UpsertKnowledgeBaseArticleRequest request,
            SupportPilotDbContext db,
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
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
            await db.SaveChangesAsync(cancellationToken);
            await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);
            var created = await db.KnowledgeBaseArticles
                .AsNoTracking()
                .Include(x => x.Category)
                .SingleAsync(x => x.Id == article.Id, cancellationToken);
            return Results.Created($"/api/kb/admin/articles/{article.Id}", ToKnowledgeBaseArticleResponse(created));
        });

        adminGroup.MapPut("/articles/{id:guid}", async (
            Guid id,
            UpsertKnowledgeBaseArticleRequest request,
            SupportPilotDbContext db,
            IApplicationCache cache,
            CancellationToken cancellationToken) =>
        {
            var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
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
            await db.SaveChangesAsync(cancellationToken);
            await cache.InvalidateGroupAsync(CacheGroups.KnowledgeBase, cancellationToken);
            var updated = await db.KnowledgeBaseArticles
                .AsNoTracking()
                .Include(x => x.Category)
                .SingleAsync(x => x.Id == id, cancellationToken);
            return Results.Ok(ToKnowledgeBaseArticleResponse(updated));
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

        group.MapGet("/overview", async (
            SupportPilotDbContext db,
            IApplicationCache cache,
            IOptions<CacheOptions> cacheOptions,
            CancellationToken cancellationToken) =>
        {
            var result = await cache.GetOrCreateAsync(
                CacheGroups.Reports,
                "overview",
                ReportsCacheExpiration(cacheOptions),
                async token =>
                {
                    var now = DateTimeOffset.UtcNow;
                    var dueSoon = now.AddHours(4);
                    var openStatuses = new[] { TicketStatus.New, TicketStatus.InProgress, TicketStatus.WaitingForCustomer };
                    var tickets = db.Tickets.AsNoTracking();

                    var byStatus = await tickets
                        .GroupBy(x => x.Status)
                        .Select(x => new DashboardBucketResponse(x.Key.ToString(), x.Count()))
                        .ToListAsync(token);
                    var byPriority = await tickets
                        .GroupBy(x => x.Priority)
                        .Select(x => new DashboardBucketResponse(x.Key.ToString(), x.Count()))
                        .ToListAsync(token);
                    var recentTickets = await db.Tickets
                        .FromSqlRaw("""
                            SELECT *
                            FROM "Tickets"
                            ORDER BY "UpdatedAt" DESC
                            LIMIT 8
                            """)
                        .AsNoTracking()
                        .Include(x => x.Category)
                        .Include(x => x.AssignedTo)
                        .Select(x => new DashboardTicketResponse(
                            x.Id,
                            x.Number,
                            x.Title,
                            x.Status,
                            x.Priority,
                            x.Category.Name,
                            x.AssignedTo == null ? null : x.AssignedTo.DisplayName,
                            x.UpdatedAt,
                            x.FirstResponseDueAt,
                            x.ResolutionDueAt,
                            x.FirstResponseBreached,
                            x.ResolutionBreached))
                        .ToListAsync(token);
                    var slaBreaches = await db.Tickets
                        .FromSqlRaw("""
                            SELECT *
                            FROM "Tickets"
                            WHERE "FirstResponseBreached" OR "ResolutionBreached"
                            ORDER BY "UpdatedAt" DESC
                            LIMIT 8
                            """)
                        .AsNoTracking()
                        .Include(x => x.Category)
                        .Include(x => x.AssignedTo)
                        .Select(x => new DashboardTicketResponse(
                            x.Id,
                            x.Number,
                            x.Title,
                            x.Status,
                            x.Priority,
                            x.Category.Name,
                            x.AssignedTo == null ? null : x.AssignedTo.DisplayName,
                            x.UpdatedAt,
                            x.FirstResponseDueAt,
                            x.ResolutionDueAt,
                            x.FirstResponseBreached,
                            x.ResolutionBreached))
                        .ToListAsync(token);
                    var overdueTickets = await db.Database
                        .SqlQuery<int>($"""
                            SELECT COUNT(*) AS "Value"
                            FROM "Tickets"
                            WHERE "Status" IN (0, 1, 2)
                              AND (
                                  ("FirstResponseDueAt" IS NOT NULL AND "FirstResponseAt" IS NULL AND "FirstResponseDueAt" < {now})
                                  OR ("ResolutionDueAt" IS NOT NULL AND "ResolvedAt" IS NULL AND "ResolutionDueAt" < {now})
                              )
                            """)
                        .SingleAsync(token);
                    var dueSoonTickets = await db.Database
                        .SqlQuery<int>($"""
                            SELECT COUNT(*) AS "Value"
                            FROM "Tickets"
                            WHERE "Status" IN (0, 1, 2)
                              AND (
                                  ("FirstResponseDueAt" IS NOT NULL AND "FirstResponseAt" IS NULL AND "FirstResponseDueAt" >= {now} AND "FirstResponseDueAt" <= {dueSoon})
                                  OR ("ResolutionDueAt" IS NOT NULL AND "ResolvedAt" IS NULL AND "ResolutionDueAt" >= {now} AND "ResolutionDueAt" <= {dueSoon})
                              )
                            """)
                        .SingleAsync(token);

                    return new DashboardOverviewResponse(
                        now,
                        await tickets.CountAsync(token),
                        await tickets.CountAsync(x => openStatuses.Contains(x.Status), token),
                        await tickets.CountAsync(x => x.Status == TicketStatus.Resolved || x.Status == TicketStatus.Closed, token),
                        await tickets.CountAsync(x => x.AssignedToId == null && openStatuses.Contains(x.Status), token),
                        overdueTickets,
                        dueSoonTickets,
                        await tickets.CountAsync(x => x.FirstResponseBreached || x.ResolutionBreached, token),
                        await tickets.CountAsync(x => openStatuses.Contains(x.Status) && x.Priority == TicketPriority.Critical, token),
                        await tickets.CountAsync(x => openStatuses.Contains(x.Status) && x.Priority == TicketPriority.High, token),
                        byStatus.OrderBy(x => x.Key).ToList(),
                        byPriority.OrderBy(x => x.Key).ToList(),
                        recentTickets,
                        slaBreaches);
                },
                cancellationToken);

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

    private static TimeSpan KnowledgeBaseCacheExpiration(IOptions<CacheOptions> options) =>
        TimeSpan.FromSeconds(NormalizeCacheSeconds(options.Value.KnowledgeBaseExpirationSeconds, 300));

    private static TimeSpan ReportsCacheExpiration(IOptions<CacheOptions> options) =>
        TimeSpan.FromSeconds(NormalizeCacheSeconds(options.Value.ReportsExpirationSeconds, 30));

    private static int NormalizeCacheSeconds(int seconds, int fallback) => seconds > 0 ? seconds : fallback;

    private static UserProfileResponse ToProfile(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.UserRoles.Select(x => x.Role.Name).Order().ToArray());

    private static RouteHandlerBuilder WithRouteDocs(
        this RouteHandlerBuilder builder,
        string summary,
        string description) =>
        builder
            .WithSummary(summary)
            .WithDescription(description)
            .WithOpenApi(operation =>
            {
                operation.Summary = summary;
                operation.Description = description;
                return operation;
            });

    private static KnowledgeBaseArticleResponse ToKnowledgeBaseArticleResponse(KnowledgeBaseArticle article) =>
        new(
            article.Id,
            article.Title,
            article.Slug,
            article.Body,
            article.CategoryId,
            article.Category.Name,
            article.IsPublished,
            article.CreatedAt,
            article.UpdatedAt);

}
