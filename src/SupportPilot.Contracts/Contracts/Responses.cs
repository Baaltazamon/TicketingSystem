using SupportPilot.Domain;

namespace SupportPilot.Contracts;

/// <summary>
/// Compact ticket representation returned by list endpoints.
/// </summary>
/// <param name="Id">Ticket identifier.</param>
/// <param name="Number">Human-readable ticket number, for example <c>SP-2026-00001</c>.</param>
/// <param name="Title">Short ticket title.</param>
/// <param name="Status">Current workflow status.</param>
/// <param name="Priority">Current business priority.</param>
/// <param name="Category">Ticket category name.</param>
/// <param name="CreatedBy">Display name of the requester.</param>
/// <param name="AssignedTo">Display name of the assigned support user, if any.</param>
/// <param name="CreatedAt">UTC timestamp when the ticket was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the ticket was last changed.</param>
/// <param name="FirstResponseDueAt">SLA deadline for first public support response.</param>
/// <param name="ResolutionDueAt">SLA deadline for resolution.</param>
/// <param name="FirstResponseBreached">Whether the first response SLA has already been breached.</param>
/// <param name="ResolutionBreached">Whether the resolution SLA has already been breached.</param>
public sealed record TicketListItemResponse(
    Guid Id,
    string Number,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    string Category,
    string CreatedBy,
    string? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? FirstResponseDueAt,
    DateTimeOffset? ResolutionDueAt,
    bool FirstResponseBreached,
    bool ResolutionBreached);

/// <summary>
/// Full ticket representation returned by the ticket details endpoint.
/// </summary>
/// <param name="Id">Ticket identifier.</param>
/// <param name="Number">Human-readable ticket number.</param>
/// <param name="Title">Short ticket title.</param>
/// <param name="Description">Full ticket description.</param>
/// <param name="Status">Current workflow status.</param>
/// <param name="Priority">Current business priority.</param>
/// <param name="CategoryId">Ticket category identifier.</param>
/// <param name="Category">Ticket category name.</param>
/// <param name="CreatedBy">Requester profile.</param>
/// <param name="AssignedTo">Assigned support user profile, if any.</param>
/// <param name="CreatedAt">UTC timestamp when the ticket was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the ticket was last changed.</param>
/// <param name="FirstResponseDueAt">SLA deadline for first public support response.</param>
/// <param name="ResolutionDueAt">SLA deadline for resolution.</param>
/// <param name="FirstResponseAt">UTC timestamp of the first public support response, if it happened.</param>
/// <param name="ResolvedAt">UTC timestamp when the ticket was resolved or closed, if it happened.</param>
/// <param name="FirstResponseBreached">Whether the first response SLA has already been breached.</param>
/// <param name="ResolutionBreached">Whether the resolution SLA has already been breached.</param>
/// <param name="Comments">Visible public comments and, for support staff, internal notes.</param>
/// <param name="Attachments">Files attached to the ticket.</param>
/// <param name="Timeline">Chronological ticket timeline assembled from status changes, comments, and attachments.</param>
public sealed record TicketDetailResponse(
    Guid Id,
    string Number,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    Guid CategoryId,
    string Category,
    UserProfileShort CreatedBy,
    UserProfileShort? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? FirstResponseDueAt,
    DateTimeOffset? ResolutionDueAt,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    bool FirstResponseBreached,
    bool ResolutionBreached,
    IEnumerable<CommentResponse> Comments,
    IEnumerable<AttachmentResponse> Attachments,
    IEnumerable<TimelineItemResponse> Timeline);

/// <summary>
/// Short user profile used inside nested ticket responses.
/// </summary>
/// <param name="Id">User identifier.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Email">User email address.</param>
public sealed record UserProfileShort(Guid Id, string DisplayName, string Email);

/// <summary>
/// Ticket comment or internal note visible to the current user.
/// </summary>
/// <param name="Id">Comment identifier.</param>
/// <param name="Body">Comment text.</param>
/// <param name="IsInternal">Whether the comment is an internal support note.</param>
/// <param name="Author">Author profile.</param>
/// <param name="CreatedAt">UTC timestamp when the comment was created.</param>
public sealed record CommentResponse(Guid Id, string Body, bool IsInternal, UserProfileShort Author, DateTimeOffset CreatedAt);

/// <summary>
/// Ticket attachment metadata.
/// </summary>
/// <param name="Id">Attachment identifier.</param>
/// <param name="FileName">Original safe file name.</param>
/// <param name="ContentType">Detected or supplied MIME content type.</param>
/// <param name="SizeBytes">Stored file size in bytes.</param>
/// <param name="DownloadUrl">API URL used to download the attachment.</param>
/// <param name="UploadedBy">Uploader profile.</param>
/// <param name="CreatedAt">UTC timestamp when the file was attached.</param>
public sealed record AttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string DownloadUrl,
    UserProfileShort UploadedBy,
    DateTimeOffset CreatedAt);

/// <summary>
/// Timeline item shown in the ticket history.
/// </summary>
/// <param name="Type">Timeline item type, for example <c>status</c>, <c>comment</c>, <c>internal-note</c>, or <c>attachment</c>.</param>
/// <param name="Text">Human-readable timeline text.</param>
/// <param name="CreatedAt">UTC timestamp of the timeline event.</param>
public sealed record TimelineItemResponse(string Type, string Text, DateTimeOffset CreatedAt);

/// <summary>
/// Response returned after successful ticket creation.
/// </summary>
/// <param name="Id">Created ticket identifier.</param>
/// <param name="Number">Created ticket number.</param>
public sealed record TicketCreatedResponse(Guid Id, string Number);

/// <summary>
/// Response returned after successful attachment upload.
/// </summary>
/// <param name="Id">Created attachment identifier.</param>
public sealed record TicketAttachmentCreatedResponse(Guid Id);
