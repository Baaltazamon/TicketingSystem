using Microsoft.AspNetCore.Mvc;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Admin;
using SupportPilot.Application.Auth;
using SupportPilot.Application.Common;
using SupportPilot.Application.KnowledgeBase;
using SupportPilot.Application.Notifications;
using SupportPilot.Application.Reports;
using SupportPilot.Application.Tickets;
using SupportPilot.Api.Auth;
using SupportPilot.Contracts;
using SupportPilot.Domain;

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

        group.MapGet("/categories", async (TicketUseCases tickets, CancellationToken cancellationToken) =>
            Results.Ok(await tickets.ListActiveCategoriesAsync(cancellationToken)));

        group.MapGet("/assignees", async (TicketUseCases tickets, CancellationToken cancellationToken) =>
            Results.Ok(await tickets.ListAssigneesAsync(cancellationToken)))
            .RequireAuthorization("SupportStaff");

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
            TicketUseCases tickets,
            CancellationToken cancellationToken) =>
        {
            var result = await tickets.DownloadAttachmentAsync(
                ticketId,
                attachmentId,
                currentUser.ToTicketActor(),
                cancellationToken);

            return result.IsSuccess
                ? Results.File(result.Value!.Content, result.Value.ContentType, result.Value.FileName)
                : ToHttpResult(result);
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

        group.MapGet("/audit", async (AdminUseCases admin, CancellationToken cancellationToken) =>
            Results.Ok(await admin.ListRecentAuditLogsAsync(cancellationToken)))
            .WithRouteDocs(
                "List recent audit events",
                "Returns the 200 most recent audit log entries with actor display names.")
            .Produces<IReadOnlyList<AuditLogResponse>>();

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

        group.MapGet("/categories", async (KnowledgeBaseUseCases knowledgeBase, CancellationToken cancellationToken) =>
            Results.Ok(await knowledgeBase.ListPublicCategoriesAsync(cancellationToken)));

        group.MapGet("/articles", async (
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
            Results.Ok(await knowledgeBase.ListPublicArticlesAsync(search, categoryId, cancellationToken)));

        group.MapGet("/articles/{slug}", async (
            string slug,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeBase.GetPublicArticleAsync(slug, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        });

        var adminGroup = group.MapGroup("/admin").RequireAuthorization("SupportStaff");

        adminGroup.MapGet("/categories", async (KnowledgeBaseUseCases knowledgeBase, CancellationToken cancellationToken) =>
            Results.Ok(await knowledgeBase.ListAdminCategoriesAsync(cancellationToken)));

        adminGroup.MapPost("/categories", async (
            UpsertKnowledgeBaseCategoryRequest request,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeBase.CreateCategoryAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/kb/admin/categories/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        adminGroup.MapPut("/categories/{id:guid}", async (
            Guid id,
            UpsertKnowledgeBaseCategoryRequest request,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeBase.UpdateCategoryAsync(id, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        });

        adminGroup.MapGet("/articles", async (
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool? published,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
            Results.Ok(await knowledgeBase.ListAdminArticlesAsync(search, categoryId, published, cancellationToken)));

        adminGroup.MapGet("/articles/{id:guid}", async (
            Guid id,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeBase.GetAdminArticleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
        });

        adminGroup.MapPost("/articles", async (
            UpsertKnowledgeBaseArticleRequest request,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeBase.CreateArticleAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/kb/admin/articles/{result.Value!.Id}", result.Value)
                : ToHttpResult(result);
        });

        adminGroup.MapPut("/articles/{id:guid}", async (
            Guid id,
            UpsertKnowledgeBaseArticleRequest request,
            KnowledgeBaseUseCases knowledgeBase,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeBase.UpdateArticleAsync(id, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ToHttpResult(result);
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

        group.MapGet("/overview", async (ReportUseCases reports, CancellationToken cancellationToken) =>
            Results.Ok(await reports.GetOverviewAsync(cancellationToken)));

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

        group.MapGet("/", async (
            CurrentUser currentUser,
            NotificationUseCases notifications,
            CancellationToken cancellationToken) =>
            Results.Ok(await notifications.ListInboxAsync(currentUser.Id, cancellationToken)))
            .WithRouteDocs(
                "List notification inbox",
                "Returns the 100 most recent personal and global notifications for the current user.")
            .Produces<IReadOnlyList<Notification>>();

        group.MapPost("/{id:guid}/read", async (
            Guid id,
            CurrentUser currentUser,
            NotificationUseCases notifications,
            CancellationToken cancellationToken) =>
        {
            var result = await notifications.MarkAsReadAsync(id, currentUser.Id, cancellationToken);
            return ToHttpResult(result);
        })
            .WithRouteDocs(
                "Mark notification as read",
                "Marks a personal notification owned by the current user as read.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

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

}
