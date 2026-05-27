using SupportPilot.Domain.Domain;

namespace SupportPilot.Contracts.Contracts;

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

public sealed record UserProfileShort(Guid Id, string DisplayName, string Email);
public sealed record CommentResponse(Guid Id, string Body, bool IsInternal, UserProfileShort Author, DateTimeOffset CreatedAt);
public sealed record AttachmentResponse(Guid Id, string FileName, string ContentType, long SizeBytes, string DownloadUrl, UserProfileShort UploadedBy, DateTimeOffset CreatedAt);
public sealed record TimelineItemResponse(string Type, string Text, DateTimeOffset CreatedAt);
