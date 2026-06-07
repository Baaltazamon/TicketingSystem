namespace SupportPilot.Domain;

/// <summary>
/// Application user account.
/// </summary>
public sealed class User
{
    /// <summary>Stable user identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Normalized email address used for login and lookup.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name shown in tickets, comments, notifications, and audit screens.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Infrastructure-specific password hash. Plain-text passwords are never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Whether the user can authenticate and participate in support workflows.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Role assignments for this user.</summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

/// <summary>
/// Application role such as Admin, Agent, or Customer.
/// </summary>
public sealed class Role
{
    /// <summary>Stable role identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique role name used by authorization policies.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>User assignments for this role.</summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

/// <summary>
/// Many-to-many join entity between users and roles.
/// </summary>
public sealed class UserRole
{
    /// <summary>User identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>User navigation property.</summary>
    public User User { get; set; } = null!;

    /// <summary>Role identifier.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Role navigation property.</summary>
    public Role Role { get; set; } = null!;
}

/// <summary>
/// Category used to classify tickets.
/// </summary>
public sealed class TicketCategory
{
    /// <summary>Stable category identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional category description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the category can be selected for new tickets.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Support ticket created by a customer or support user.
/// </summary>
public sealed class Ticket
{
    /// <summary>Stable ticket identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable ticket number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Short ticket title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed ticket description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Current workflow status.</summary>
    public TicketStatus Status { get; set; } = TicketStatus.New;

    /// <summary>Business priority used for SLA policy selection.</summary>
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    /// <summary>Ticket category identifier.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Ticket category navigation property.</summary>
    public TicketCategory Category { get; set; } = null!;

    /// <summary>Requester user identifier.</summary>
    public Guid CreatedById { get; set; }

    /// <summary>Requester navigation property.</summary>
    public User CreatedBy { get; set; } = null!;

    /// <summary>Assigned support user identifier, if the ticket is assigned.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Assigned support user navigation property.</summary>
    public User? AssignedTo { get; set; }

    /// <summary>UTC timestamp when the ticket was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the ticket was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>SLA deadline for first public support response.</summary>
    public DateTimeOffset? FirstResponseDueAt { get; set; }

    /// <summary>SLA deadline for resolution.</summary>
    public DateTimeOffset? ResolutionDueAt { get; set; }

    /// <summary>UTC timestamp when support first replied publicly.</summary>
    public DateTimeOffset? FirstResponseAt { get; set; }

    /// <summary>UTC timestamp when the ticket was resolved or closed.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Whether the first response SLA deadline was breached.</summary>
    public bool FirstResponseBreached { get; set; }

    /// <summary>Whether the resolution SLA deadline was breached.</summary>
    public bool ResolutionBreached { get; set; }

    /// <summary>Ticket comments and internal notes.</summary>
    public ICollection<TicketComment> Comments { get; set; } = [];

    /// <summary>Files attached to the ticket.</summary>
    public ICollection<TicketAttachment> Attachments { get; set; } = [];

    /// <summary>Workflow status history.</summary>
    public ICollection<TicketStatusHistory> StatusHistory { get; set; } = [];
}

/// <summary>
/// Public comment or internal support note attached to a ticket.
/// </summary>
public sealed class TicketComment
{
    /// <summary>Stable comment identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent ticket identifier.</summary>
    public Guid TicketId { get; set; }

    /// <summary>Parent ticket navigation property.</summary>
    public Ticket Ticket { get; set; } = null!;

    /// <summary>Author user identifier.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Author navigation property.</summary>
    public User Author { get; set; } = null!;

    /// <summary>Comment body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Whether this comment is visible only to support staff.</summary>
    public bool IsInternal { get; set; }

    /// <summary>UTC timestamp when the comment was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Metadata for a file attached to a ticket.
/// </summary>
public sealed class TicketAttachment
{
    /// <summary>Stable attachment identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent ticket identifier.</summary>
    public Guid TicketId { get; set; }

    /// <summary>Parent ticket navigation property.</summary>
    public Ticket Ticket { get; set; } = null!;

    /// <summary>Uploader user identifier.</summary>
    public Guid UploadedById { get; set; }

    /// <summary>Uploader navigation property.</summary>
    public User UploadedBy { get; set; } = null!;

    /// <summary>Original safe file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Stored file size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Storage provider key used to retrieve the file from local storage or MinIO.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the file was attached.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Single ticket workflow transition.
/// </summary>
public sealed class TicketStatusHistory
{
    /// <summary>Stable history item identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ticket identifier.</summary>
    public Guid TicketId { get; set; }

    /// <summary>Ticket navigation property.</summary>
    public Ticket Ticket { get; set; } = null!;

    /// <summary>Previous status, or null for ticket creation.</summary>
    public TicketStatus? FromStatus { get; set; }

    /// <summary>New status after the transition.</summary>
    public TicketStatus ToStatus { get; set; }

    /// <summary>User identifier that performed the transition.</summary>
    public Guid ChangedById { get; set; }

    /// <summary>User navigation property that performed the transition.</summary>
    public User ChangedBy { get; set; } = null!;

    /// <summary>Optional transition reason.</summary>
    public string? Reason { get; set; }

    /// <summary>UTC timestamp when the transition happened.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// SLA policy selected by ticket priority.
/// </summary>
public sealed class SlaPolicy
{
    /// <summary>Stable policy identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Policy name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Priority covered by this policy.</summary>
    public TicketPriority Priority { get; set; }

    /// <summary>Allowed time to first response, in minutes.</summary>
    public int FirstResponseMinutes { get; set; }

    /// <summary>Allowed time to resolution, in minutes.</summary>
    public int ResolutionMinutes { get; set; }

    /// <summary>Whether this policy is used for new tickets.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Notification stored for a user or broadcast to all users.
/// </summary>
public sealed class Notification
{
    /// <summary>Stable notification identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Recipient user identifier. Null means the notification is global.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Recipient navigation property.</summary>
    public User? User { get; set; }

    /// <summary>Related ticket identifier, if any.</summary>
    public Guid? TicketId { get; set; }

    /// <summary>Related ticket navigation property.</summary>
    public Ticket? Ticket { get; set; }

    /// <summary>Notification type.</summary>
    public NotificationType Type { get; set; }

    /// <summary>Human-readable notification text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Whether the recipient has marked the notification as read.</summary>
    public bool IsRead { get; set; }

    /// <summary>UTC timestamp when the notification was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Knowledge base category.
/// </summary>
public sealed class KnowledgeBaseCategory
{
    /// <summary>Stable category identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional category description.</summary>
    public string? Description { get; set; }

    /// <summary>Articles assigned to this category.</summary>
    public ICollection<KnowledgeBaseArticle> Articles { get; set; } = [];
}

/// <summary>
/// Knowledge base article used for FAQ and self-service support.
/// </summary>
public sealed class KnowledgeBaseArticle
{
    /// <summary>Stable article identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Category identifier.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Category navigation property.</summary>
    public KnowledgeBaseCategory Category { get; set; } = null!;

    /// <summary>Article title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>URL-friendly unique slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Article body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Whether customers can read this article.</summary>
    public bool IsPublished { get; set; }

    /// <summary>UTC timestamp when the article was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the article was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Immutable audit event that records security-sensitive or business-significant changes.
/// </summary>
public sealed class AuditLog
{
    /// <summary>Stable audit event identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User identifier that performed the action, if known.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Actor navigation property.</summary>
    public User? Actor { get; set; }

    /// <summary>Audited action type.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Name of the affected entity type.</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Identifier of the affected entity serialized as text.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Additional audit details.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the audit event was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
