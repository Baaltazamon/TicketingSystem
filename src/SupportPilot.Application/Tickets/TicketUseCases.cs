using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Common;
using SupportPilot.Application.Notifications;
using SupportPilot.Contracts;
using SupportPilot.Domain;

namespace SupportPilot.Application.Tickets;

/// <summary>
/// Ticket workflow use cases used by the API layer.
/// </summary>
public sealed class TicketUseCases(
    ISupportPilotDbContext db,
    IFileStorage fileStorage,
    ITicketRealtimeNotifier realtimeNotifier,
    INotificationPublisher notificationPublisher)
{
    /// <summary>
    /// Lists active ticket categories available for new tickets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active categories sorted by name.</returns>
    public async Task<IReadOnlyList<TicketCategory>> ListActiveCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.TicketCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lists active support users that can be assigned to tickets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active administrators and agents sorted by display name.</returns>
    public async Task<IReadOnlyList<UserProfileResponse>> ListAssigneesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Where(x => x.IsActive && x.UserRoles.Any(role => role.Role.Name == "Admin" || role.Role.Name == "Agent"))
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);

        return users
            .Select(x => new UserProfileResponse(
                x.Id,
                x.Email,
                x.DisplayName,
                x.UserRoles.Select(role => role.Role.Name).Order().ToArray()))
            .ToList();
    }

    /// <summary>
    /// Lists tickets visible to the current actor with optional support filters.
    /// </summary>
    /// <param name="query">Ticket query filters.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>At most 100 visible tickets ordered by recent activity.</returns>
    public async Task<IReadOnlyList<TicketListItemResponse>> ListAsync(
        TicketQuery query,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var tickets = db.Tickets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.CreatedBy)
            .Include(x => x.AssignedTo)
            .AsQueryable();

        if (!actor.IsSupportStaff)
        {
            tickets = tickets.Where(x => x.CreatedById == actor.Id);
        }

        if (actor.IsSupportStaff && query.Mine == true)
        {
            tickets = tickets.Where(x => x.AssignedToId == actor.Id);
        }

        if (actor.IsSupportStaff && query.Unassigned == true)
        {
            tickets = tickets.Where(x => x.AssignedToId == null);
        }

        if (query.Overdue == true)
        {
            var now = DateTimeOffset.UtcNow;
            tickets = tickets.Where(x =>
                x.Status != TicketStatus.Resolved &&
                x.Status != TicketStatus.Closed &&
                x.Status != TicketStatus.Cancelled &&
                ((x.FirstResponseDueAt != null && x.FirstResponseAt == null && x.FirstResponseDueAt < now) ||
                 (x.ResolutionDueAt != null && x.ResolvedAt == null && x.ResolutionDueAt < now)));
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

        var filteredTickets = await tickets.ToListAsync(cancellationToken);

        return filteredTickets
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
            .ToList();
    }

    /// <summary>
    /// Creates a new ticket and initializes SLA deadlines from the active priority policy.
    /// </summary>
    /// <param name="request">Ticket creation request.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created ticket identifier and number, or validation error.</returns>
    public async Task<ApplicationResult<TicketCreatedResponse>> CreateAsync(
        CreateTicketRequest request,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        if (!await db.TicketCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken))
        {
            return ApplicationResult<TicketCreatedResponse>.Failure(
                ApplicationError.Validation,
                "Категория не найдена или неактивна.");
        }

        var policy = await db.SlaPolicies.SingleOrDefaultAsync(x => x.Priority == request.Priority && x.IsActive, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var ticket = new Ticket
        {
            Number = await NextTicketNumberAsync(cancellationToken),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryId = request.CategoryId,
            Priority = request.Priority,
            CreatedById = actor.Id,
            CreatedAt = now,
            UpdatedAt = now,
            FirstResponseDueAt = policy is null ? null : now.AddMinutes(policy.FirstResponseMinutes),
            ResolutionDueAt = policy is null ? null : now.AddMinutes(policy.ResolutionMinutes)
        };
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            Ticket = ticket,
            ToStatus = TicketStatus.New,
            ChangedById = actor.Id,
            Reason = "Ticket created",
            CreatedAt = now
        });

        db.Tickets.Add(ticket);
        AddAudit(actor.Id, AuditAction.Created, nameof(Ticket), ticket.Id, $"Created ticket {ticket.Number}");
        await db.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.TicketCreatedAsync(ticket.Id, ticket.Number, ticket.Title, cancellationToken);

        return ApplicationResult<TicketCreatedResponse>.Success(new TicketCreatedResponse(ticket.Id, ticket.Number));
    }

    /// <summary>
    /// Loads full ticket details, including comments, attachments, and timeline visible to the actor.
    /// </summary>
    /// <param name="id">Ticket identifier.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ticket details, not found, or forbidden result.</returns>
    public async Task<ApplicationResult<TicketDetailResponse>> GetDetailsAsync(
        Guid id,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var ticket = await LoadTicketDetails(id, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult<TicketDetailResponse>.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (!CanReadTicket(actor, ticket))
        {
            return ApplicationResult<TicketDetailResponse>.Failure(ApplicationError.Forbidden, "Нет доступа к обращению.");
        }

        return ApplicationResult<TicketDetailResponse>.Success(ToDetail(ticket, actor));
    }

    /// <summary>
    /// Changes ticket status according to workflow and actor permissions.
    /// </summary>
    /// <param name="id">Ticket identifier.</param>
    /// <param name="request">Requested status transition.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or a normalized application error.</returns>
    public async Task<ApplicationResult> ChangeStatusAsync(
        Guid id,
        UpdateTicketStatusRequest request,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (!CanReadTicket(actor, ticket))
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden, "Нет доступа к обращению.");
        }

        if (!TicketWorkflow.CanTransition(ticket.Status, request.Status, actor))
        {
            var allowed = string.Join(", ", TicketWorkflow.GetAllowedTransitions(ticket.Status, actor));
            return ApplicationResult.Failure(
                ApplicationError.Validation,
                allowed.Length == 0
                    ? $"Переход из статуса {ticket.Status} запрещен."
                    : $"Переход {ticket.Status} -> {request.Status} запрещен. Доступные переходы: {allowed}.");
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
            ChangedById = actor.Id,
            Reason = request.Reason,
            CreatedAt = now
        });
        NotificationMessage? notification = null;
        if (ticket.CreatedById != actor.Id)
        {
            notification = new NotificationMessage(
                ticket.CreatedById,
                ticket.Id,
                NotificationType.TicketUpdated,
                $"Статус обращения {ticket.Number} изменен: {previous} -> {request.Status}");
        }

        AddAudit(actor.Id, AuditAction.StatusChanged, nameof(Ticket), ticket.Id, $"{previous} -> {request.Status}");
        await db.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationPublisher.PublishAsync(notification, cancellationToken);
        }

        await realtimeNotifier.TicketUpdatedAsync(ticket.Id, new { ticket.Id, ticket.Status }, cancellationToken);

        return ApplicationResult.Success();
    }

    /// <summary>
    /// Assigns or unassigns a ticket to a support user.
    /// </summary>
    /// <param name="id">Ticket identifier.</param>
    /// <param name="request">Assignment request.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or a normalized application error.</returns>
    public async Task<ApplicationResult> AssignAsync(
        Guid id,
        AssignTicketRequest request,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsSupportStaff)
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden, "Назначать ответственного может только сотрудник поддержки.");
        }

        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (request.AssignedToId.HasValue && !await UserHasRoleAsync(request.AssignedToId.Value, cancellationToken, "Agent", "Admin"))
        {
            return ApplicationResult.Failure(
                ApplicationError.Validation,
                "Назначить можно только агента или администратора.");
        }

        ticket.AssignedToId = request.AssignedToId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        NotificationMessage? notification = null;
        if (request.AssignedToId.HasValue)
        {
            notification = new NotificationMessage(
                request.AssignedToId,
                ticket.Id,
                NotificationType.TicketAssigned,
                $"Вам назначено обращение {ticket.Number}");
        }

        AddAudit(actor.Id, AuditAction.Assigned, nameof(Ticket), ticket.Id, $"Assigned to {request.AssignedToId}");
        await db.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await notificationPublisher.PublishAsync(notification, cancellationToken);
        }

        await realtimeNotifier.TicketAssignedAsync(ticket.Id, ticket.AssignedToId, cancellationToken);

        return ApplicationResult.Success();
    }

    /// <summary>
    /// Adds a public comment or internal note to a ticket.
    /// </summary>
    /// <param name="id">Ticket identifier.</param>
    /// <param name="request">Comment request.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or a normalized application error.</returns>
    public async Task<ApplicationResult> AddCommentAsync(
        Guid id,
        CreateCommentRequest request,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (!CanReadTicket(actor, ticket))
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden, "Нет доступа к обращению.");
        }

        if (request.IsInternal && !actor.IsSupportStaff)
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden, "Внутренние заметки доступны только сотрудникам поддержки.");
        }

        var now = DateTimeOffset.UtcNow;
        if (actor.IsSupportStaff && !request.IsInternal)
        {
            ticket.FirstResponseAt ??= now;
        }

        ticket.UpdatedAt = now;
        db.TicketComments.Add(new TicketComment
        {
            TicketId = ticket.Id,
            AuthorId = actor.Id,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = now
        });
        var notification = new NotificationMessage(
            ticket.CreatedById == actor.Id ? ticket.AssignedToId : ticket.CreatedById,
            ticket.Id,
            NotificationType.CommentAdded,
            $"Новый комментарий в обращении {ticket.Number}");

        AddAudit(actor.Id, AuditAction.Commented, nameof(Ticket), ticket.Id, request.IsInternal ? "Internal note added" : "Public comment added");
        await db.SaveChangesAsync(cancellationToken);
        await notificationPublisher.PublishAsync(notification, cancellationToken);
        await realtimeNotifier.CommentAddedAsync(ticket.Id, cancellationToken);

        return ApplicationResult.Success();
    }

    /// <summary>
    /// Stores an attachment file and links its metadata to a ticket.
    /// </summary>
    /// <param name="id">Ticket identifier.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="content">Attachment content stream.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created attachment identifier or a normalized application error.</returns>
    public async Task<ApplicationResult<TicketAttachmentCreatedResponse>> UploadAttachmentAsync(
        Guid id,
        string fileName,
        string contentType,
        Stream content,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult<TicketAttachmentCreatedResponse>.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (!CanReadTicket(actor, ticket))
        {
            return ApplicationResult<TicketAttachmentCreatedResponse>.Failure(ApplicationError.Forbidden, "Нет доступа к обращению.");
        }

        var saved = await fileStorage.SaveAsync(ticket.Id, fileName, content, cancellationToken);
        var attachment = new TicketAttachment
        {
            TicketId = ticket.Id,
            UploadedById = actor.Id,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = saved.SizeBytes,
            StorageKey = saved.StorageKey
        };
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        db.TicketAttachments.Add(attachment);
        AddAudit(actor.Id, AuditAction.AttachmentUploaded, nameof(Ticket), ticket.Id, attachment.FileName);
        await db.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.AttachmentUploadedAsync(ticket.Id, attachment.Id, cancellationToken);

        return ApplicationResult<TicketAttachmentCreatedResponse>.Success(new TicketAttachmentCreatedResponse(attachment.Id));
    }

    /// <summary>
    /// Opens an attachment for download after validating ticket read access.
    /// </summary>
    /// <param name="ticketId">Ticket identifier.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attachment download stream or a normalized application error.</returns>
    public async Task<ApplicationResult<TicketAttachmentDownload>> DownloadAttachmentAsync(
        Guid ticketId,
        Guid attachmentId,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult<TicketAttachmentDownload>.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (!CanReadTicket(actor, ticket))
        {
            return ApplicationResult<TicketAttachmentDownload>.Failure(ApplicationError.Forbidden, "Нет доступа к обращению.");
        }

        var attachment = await db.TicketAttachments.SingleOrDefaultAsync(
            x => x.Id == attachmentId && x.TicketId == ticketId,
            cancellationToken);
        if (attachment is null)
        {
            return ApplicationResult<TicketAttachmentDownload>.Failure(ApplicationError.NotFound, "Вложение не найдено.");
        }

        var download = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        if (download is null)
        {
            return ApplicationResult<TicketAttachmentDownload>.Failure(
                ApplicationError.NotFound,
                "Файл отсутствует в хранилище.");
        }

        return ApplicationResult<TicketAttachmentDownload>.Success(new TicketAttachmentDownload(
            download.Content,
            attachment.ContentType,
            attachment.FileName,
            download.SizeBytes));
    }

    /// <summary>
    /// Deletes an attachment metadata record and removes the object from configured file storage.
    /// </summary>
    /// <param name="ticketId">Ticket identifier.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="actor">Current actor authorization context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or a normalized application error.</returns>
    public async Task<ApplicationResult> DeleteAttachmentAsync(
        Guid ticketId,
        Guid attachmentId,
        TicketActor actor,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound, "Обращение не найдено.");
        }

        if (!CanReadTicket(actor, ticket))
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden, "Нет доступа к обращению.");
        }

        var attachment = await db.TicketAttachments.SingleOrDefaultAsync(
            x => x.Id == attachmentId && x.TicketId == ticketId,
            cancellationToken);
        if (attachment is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound, "Вложение не найдено.");
        }

        if (!actor.IsSupportStaff && attachment.UploadedById != actor.Id)
        {
            return ApplicationResult.Failure(ApplicationError.Forbidden, "Удалить можно только собственное вложение.");
        }

        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        db.TicketAttachments.Remove(attachment);
        AddAudit(actor.Id, AuditAction.Deleted, nameof(TicketAttachment), attachment.Id, attachment.FileName);
        await db.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    private async Task<bool> UserHasRoleAsync(Guid userId, CancellationToken cancellationToken, params string[] roles) =>
        await db.UserRoles.AnyAsync(x => x.UserId == userId && roles.Contains(x.Role.Name), cancellationToken);

    private static bool CanReadTicket(TicketActor actor, Ticket ticket) =>
        actor.IsSupportStaff || ticket.CreatedById == actor.Id;

    private async Task<Ticket?> LoadTicketDetails(Guid id, CancellationToken cancellationToken) =>
        await db.Tickets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.CreatedBy)
            .Include(x => x.AssignedTo)
            .Include(x => x.Comments).ThenInclude(x => x.Author)
            .Include(x => x.Attachments).ThenInclude(x => x.UploadedBy)
            .Include(x => x.StatusHistory).ThenInclude(x => x.ChangedBy)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    private static TicketDetailResponse ToDetail(Ticket ticket, TicketActor actor)
    {
        var comments = ticket.Comments
            .Where(x => actor.CanSeeInternalNotes || !x.IsInternal)
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

    private static UserProfileShort ToShort(User user) => new(user.Id, user.DisplayName, user.Email);

    private void AddAudit(Guid actorId, AuditAction action, string entityName, Guid entityId, string details) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Details = details
        });

    private async Task<string> NextTicketNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var prefix = $"SP-{year}-";
        var count = await db.Tickets.CountAsync(x => x.Number.StartsWith(prefix), cancellationToken) + 1;
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}{count:00000}");
    }
}

/// <summary>
/// Attachment file opened for HTTP download.
/// </summary>
/// <param name="Content">Readable file content stream.</param>
/// <param name="ContentType">Attachment MIME content type.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="SizeBytes">Stored file size, if known.</param>
public sealed record TicketAttachmentDownload(
    Stream Content,
    string ContentType,
    string FileName,
    long? SizeBytes) : IAsyncDisposable
{
    /// <summary>Disposes the wrapped content stream.</summary>
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
