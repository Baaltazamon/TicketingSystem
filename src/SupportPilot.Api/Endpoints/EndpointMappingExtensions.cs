using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Api.Auth;
using SupportPilot.Api.Realtime;
using SupportPilot.Contracts;
using SupportPilot.Contracts.Contracts;
using SupportPilot.Domain;
using SupportPilot.Domain.Domain;
using SupportPilot.Infrastructure.Auth;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Api.Endpoints;

public static class EndpointMappingExtensions
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, SupportPilotDbContext db) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Email == email))
            {
                return Results.Conflict(new { message = "Пользователь с таким email уже существует." });
            }

            var customerRole = await db.Roles.SingleAsync(x => x.Name == "Customer");
            var user = new User
            {
                Email = email,
                DisplayName = request.DisplayName.Trim()
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
            user.UserRoles.Add(new UserRole { User = user, Role = customerRole });

            db.Users.Add(user);
            db.AuditLogs.Add(new AuditLog
            {
                ActorId = user.Id,
                Action = AuditAction.Created,
                EntityName = nameof(User),
                EntityId = user.Id.ToString(),
                Details = $"Registered user {email}"
            });
            await db.SaveChangesAsync();

            return Results.Created($"/api/users/{user.Id}", ToProfile(user));
        });

        group.MapPost("/login", async (LoginRequest request, SupportPilotDbContext db, JwtTokenService tokens) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .SingleOrDefaultAsync(x => x.Email == email && x.IsActive);

            if (user is null)
            {
                return Results.Unauthorized();
            }

            var result = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new AuthResponse(tokens.CreateToken(user), ToProfile(user)));
        });

        group.MapGet("/me", async (CurrentUser currentUser, SupportPilotDbContext db) =>
        {
            var user = await db.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .SingleOrDefaultAsync(x => x.Id == currentUser.Id);

            return user is null ? Results.Unauthorized() : Results.Ok(ToProfile(user));
        }).RequireAuthorization();

        return app;
    }

    public static WebApplication MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets").RequireAuthorization();

        group.MapGet("/", async ([AsParameters] TicketQuery query, CurrentUser currentUser, SupportPilotDbContext db) =>
        {
            var tickets = db.Tickets
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.CreatedBy)
                .Include(x => x.AssignedTo)
                .AsQueryable();

            if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("Agent"))
            {
                tickets = tickets.Where(x => x.CreatedById == currentUser.Id);
            }

            if (query.Status.HasValue)
            {
                tickets = tickets.Where(x => x.Status == query.Status.Value);
            }

            if (query.Priority.HasValue)
            {
                tickets = tickets.Where(x => x.Priority == query.Priority.Value);
            }

            if (query.CategoryId.HasValue)
            {
                tickets = tickets.Where(x => x.CategoryId == query.CategoryId.Value);
            }

            if (query.AssignedToId.HasValue)
            {
                tickets = tickets.Where(x => x.AssignedToId == query.AssignedToId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                tickets = tickets.Where(x => x.Title.Contains(search) || x.Description.Contains(search) || x.Number.Contains(search));
            }

            var result = await tickets
                .OrderByDescending(x => x.UpdatedAt)
                .Take(100)
                .Select(x => new TicketListItemResponse(
                    x.Id,
                    x.Number,
                    x.Title,
                    x.Status,
                    x.Priority,
                    x.Category.Name,
                    x.CreatedBy.DisplayName,
                    x.AssignedTo == null ? null : x.AssignedTo.DisplayName,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.FirstResponseDueAt,
                    x.ResolutionDueAt,
                    x.FirstResponseBreached,
                    x.ResolutionBreached))
                .ToListAsync();

            return Results.Ok(result);
        });

        group.MapPost("/", async (
            CreateTicketRequest request,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IHubContext<TicketHub> hubContext) =>
        {
            if (!await db.TicketCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive))
            {
                return Results.BadRequest(new { message = "Категория не найдена или неактивна." });
            }

            var policy = await db.SlaPolicies.SingleOrDefaultAsync(x => x.Priority == request.Priority && x.IsActive);
            var now = DateTimeOffset.UtcNow;
            var ticket = new Ticket
            {
                Number = await NextTicketNumberAsync(db),
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                CategoryId = request.CategoryId,
                Priority = request.Priority,
                CreatedById = currentUser.Id,
                CreatedAt = now,
                UpdatedAt = now,
                FirstResponseDueAt = policy is null ? null : now.AddMinutes(policy.FirstResponseMinutes),
                ResolutionDueAt = policy is null ? null : now.AddMinutes(policy.ResolutionMinutes)
            };
            ticket.StatusHistory.Add(new TicketStatusHistory
            {
                Ticket = ticket,
                ToStatus = TicketStatus.New,
                ChangedById = currentUser.Id,
                Reason = "Ticket created",
                CreatedAt = now
            });

            db.Tickets.Add(ticket);
            AddAudit(db, currentUser.Id, AuditAction.Created, nameof(Ticket), ticket.Id, $"Created ticket {ticket.Number}");
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("ticketCreated", new { ticket.Id, ticket.Number, ticket.Title });

            return Results.Created($"/api/tickets/{ticket.Id}", new { ticket.Id, ticket.Number });
        });

        group.MapGet("/{id:guid}", async (Guid id, CurrentUser currentUser, SupportPilotDbContext db) =>
        {
            var ticket = await LoadTicketDetails(db, id);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!CanReadTicket(currentUser, ticket))
            {
                return Results.Forbid();
            }

            return Results.Ok(ToDetail(ticket, currentUser));
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            UpdateTicketStatusRequest request,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IHubContext<TicketHub> hubContext) =>
        {
            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("Agent") && ticket.CreatedById != currentUser.Id)
            {
                return Results.Forbid();
            }

            var previous = ticket.Status;
            var now = DateTimeOffset.UtcNow;
            ticket.Status = request.Status;
            ticket.UpdatedAt = now;
            if (request.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                ticket.ResolvedAt ??= now;
            }

            db.TicketStatusHistory.Add(new TicketStatusHistory
            {
                TicketId = ticket.Id,
                FromStatus = previous,
                ToStatus = request.Status,
                ChangedById = currentUser.Id,
                Reason = request.Reason,
                CreatedAt = now
            });
            AddAudit(db, currentUser.Id, AuditAction.StatusChanged, nameof(Ticket), ticket.Id, $"{previous} -> {request.Status}");
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("ticketUpdated", new { ticket.Id, ticket.Status });

            return Results.NoContent();
        });

        group.MapPatch("/{id:guid}/assignee", async (
            Guid id,
            AssignTicketRequest request,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IHubContext<TicketHub> hubContext) =>
        {
            if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("Agent"))
            {
                return Results.Forbid();
            }

            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (request.AssignedToId.HasValue && !await UserHasRoleAsync(db, request.AssignedToId.Value, "Agent", "Admin"))
            {
                return Results.BadRequest(new { message = "Назначить можно только агента или администратора." });
            }

            ticket.AssignedToId = request.AssignedToId;
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            if (request.AssignedToId.HasValue)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = request.AssignedToId,
                    TicketId = ticket.Id,
                    Type = NotificationType.TicketAssigned,
                    Message = $"Вам назначено обращение {ticket.Number}"
                });
            }

            AddAudit(db, currentUser.Id, AuditAction.Assigned, nameof(Ticket), ticket.Id, $"Assigned to {request.AssignedToId}");
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("ticketAssigned", new { ticket.Id, ticket.AssignedToId });

            return Results.NoContent();
        }).RequireAuthorization("SupportStaff");

        group.MapPost("/{id:guid}/comments", async (
            Guid id,
            CreateCommentRequest request,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IHubContext<TicketHub> hubContext) =>
        {
            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!CanReadTicket(currentUser, ticket))
            {
                return Results.Forbid();
            }

            if (request.IsInternal && !currentUser.IsInRole("Admin") && !currentUser.IsInRole("Agent"))
            {
                return Results.Forbid();
            }

            var now = DateTimeOffset.UtcNow;
            var isSupportReply = currentUser.IsInRole("Admin") || currentUser.IsInRole("Agent");
            if (isSupportReply && !request.IsInternal)
            {
                ticket.FirstResponseAt ??= now;
            }

            ticket.UpdatedAt = now;
            db.TicketComments.Add(new TicketComment
            {
                TicketId = ticket.Id,
                AuthorId = currentUser.Id,
                Body = request.Body.Trim(),
                IsInternal = request.IsInternal,
                CreatedAt = now
            });
            db.Notifications.Add(new Notification
            {
                UserId = ticket.CreatedById == currentUser.Id ? ticket.AssignedToId : ticket.CreatedById,
                TicketId = ticket.Id,
                Type = NotificationType.CommentAdded,
                Message = $"Новый комментарий в обращении {ticket.Number}"
            });

            AddAudit(db, currentUser.Id, AuditAction.Commented, nameof(Ticket), ticket.Id, request.IsInternal ? "Internal note added" : "Public comment added");
            await db.SaveChangesAsync();
            await hubContext.Clients.All.SendAsync("commentAdded", new { ticket.Id });

            return Results.Created($"/api/tickets/{ticket.Id}/comments", new { ticket.Id });
        });

        group.MapPost("/{id:guid}/attachments", async (
            Guid id,
            HttpRequest request,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IFileStorage storage,
            IHubContext<TicketHub> hubContext,
            CancellationToken cancellationToken) =>
        {
            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!CanReadTicket(currentUser, ticket))
            {
                return Results.Forbid();
            }

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
            var saved = await storage.SaveAsync(ticket.Id, file.FileName, fileStream, cancellationToken);
            var attachment = new TicketAttachment
            {
                TicketId = ticket.Id,
                UploadedById = currentUser.Id,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                SizeBytes = saved.SizeBytes,
                StorageKey = saved.StorageKey
            };
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            db.TicketAttachments.Add(attachment);
            AddAudit(db, currentUser.Id, AuditAction.AttachmentUploaded, nameof(Ticket), ticket.Id, attachment.FileName);
            await db.SaveChangesAsync(cancellationToken);
            await hubContext.Clients.All.SendAsync(
                "attachmentUploaded",
                new { TicketId = ticket.Id, AttachmentId = attachment.Id },
                cancellationToken);

            return Results.Created($"/api/tickets/{ticket.Id}/attachments/{attachment.Id}", new { attachment.Id });
        });

        group.MapGet("/{ticketId:guid}/attachments/{attachmentId:guid}", async (
            Guid ticketId,
            Guid attachmentId,
            CurrentUser currentUser,
            SupportPilotDbContext db,
            IFileStorage storage) =>
        {
            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (!CanReadTicket(currentUser, ticket))
            {
                return Results.Forbid();
            }

            var attachment = await db.TicketAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.TicketId == ticketId);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            var path = storage.GetFullPath(attachment.StorageKey);
            return File.Exists(path)
                ? Results.File(path, attachment.ContentType, attachment.FileName)
                : Results.NotFound(new { message = "Файл отсутствует в хранилище." });
        });

        return app;
    }

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
            var logs = await db.AuditLogs
                .AsNoTracking()
                .Include(x => x.Actor)
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
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

            return Results.Ok(logs);
        });

        return app;
    }

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

    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async (CurrentUser currentUser, SupportPilotDbContext db) =>
        {
            var notifications = await db.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == currentUser.Id || x.UserId == null)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
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

    private static async Task<bool> UserHasRoleAsync(SupportPilotDbContext db, Guid userId, params string[] roles) =>
        await db.UserRoles.AnyAsync(x => x.UserId == userId && roles.Contains(x.Role.Name));

    private static bool CanReadTicket(CurrentUser currentUser, Ticket ticket) =>
        currentUser.IsInRole("Admin") || currentUser.IsInRole("Agent") || ticket.CreatedById == currentUser.Id;

    private static async Task<Ticket?> LoadTicketDetails(SupportPilotDbContext db, Guid id) =>
        await db.Tickets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.CreatedBy)
            .Include(x => x.AssignedTo)
            .Include(x => x.Comments).ThenInclude(x => x.Author)
            .Include(x => x.Attachments).ThenInclude(x => x.UploadedBy)
            .Include(x => x.StatusHistory).ThenInclude(x => x.ChangedBy)
            .SingleOrDefaultAsync(x => x.Id == id);

    private static TicketDetailResponse ToDetail(Ticket ticket, CurrentUser currentUser)
    {
        var canSeeInternal = currentUser.IsInRole("Admin") || currentUser.IsInRole("Agent");
        var comments = ticket.Comments
            .Where(x => canSeeInternal || !x.IsInternal)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new CommentResponse(x.Id, x.Body, x.IsInternal, ToShort(x.Author), x.CreatedAt))
            .ToList();
        var attachments = ticket.Attachments
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AttachmentResponse(
                x.Id,
                x.FileName,
                x.ContentType,
                x.SizeBytes,
                $"/api/tickets/{ticket.Id}/attachments/{x.Id}",
                ToShort(x.UploadedBy),
                x.CreatedAt))
            .ToList();
        var timeline = ticket.StatusHistory
            .Select(x => new TimelineItemResponse(
                "status",
                x.FromStatus is null
                    ? $"Ticket created with status {x.ToStatus}"
                    : $"Status changed: {x.FromStatus} -> {x.ToStatus}",
                x.CreatedAt))
            .Concat(comments.Select(x => new TimelineItemResponse(
                x.IsInternal ? "internal-note" : "comment",
                x.IsInternal ? "Internal note added" : "Comment added",
                x.CreatedAt)))
            .Concat(attachments.Select(x => new TimelineItemResponse("attachment", $"File attached: {x.FileName}", x.CreatedAt)))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        return new TicketDetailResponse(
            ticket.Id,
            ticket.Number,
            ticket.Title,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.CategoryId,
            ticket.Category.Name,
            ToShort(ticket.CreatedBy),
            ticket.AssignedTo is null ? null : ToShort(ticket.AssignedTo),
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.FirstResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.FirstResponseAt,
            ticket.ResolvedAt,
            ticket.FirstResponseBreached,
            ticket.ResolutionBreached,
            comments,
            attachments,
            timeline);
    }

    private static UserProfileResponse ToProfile(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.UserRoles.Select(x => x.Role.Name).Order().ToArray());

    private static UserProfileShort ToShort(User user) => new(user.Id, user.DisplayName, user.Email);

    private static void AddAudit(SupportPilotDbContext db, Guid actorId, AuditAction action, string entityName, Guid entityId, string details) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Details = details
        });

    private static async Task<string> NextTicketNumberAsync(SupportPilotDbContext db)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var prefix = $"SP-{year}-";
        var count = await db.Tickets.CountAsync(x => x.Number.StartsWith(prefix)) + 1;
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}{count:00000}");
    }
}
