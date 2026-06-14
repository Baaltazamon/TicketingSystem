using SupportPilot.Domain;

namespace SupportPilot.Contracts;

/// <summary>
/// Request used to create a new customer account.
/// </summary>
/// <param name="Email">User email address. The application normalizes it to lower-case before persistence.</param>
/// <param name="DisplayName">Human-readable display name shown in tickets, comments, audit records, and notifications.</param>
/// <param name="Password">Plain-text password supplied only during registration. It is hashed in Infrastructure before storage.</param>
public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>
/// Request used to authenticate an existing active user.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Password">Plain-text password to verify against the stored password hash.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Successful authentication response containing a JWT and the current user profile.
/// </summary>
/// <param name="Token">Bearer token accepted by protected API endpoints.</param>
/// <param name="User">Authenticated user profile and role list.</param>
public sealed record AuthResponse(string Token, UserProfileResponse User);

/// <summary>
/// Public user profile returned by authentication and user endpoints.
/// </summary>
/// <param name="Id">Stable user identifier.</param>
/// <param name="Email">Normalized user email address.</param>
/// <param name="DisplayName">Display name shown in support workflows.</param>
/// <param name="Roles">Application roles assigned to the user, for example <c>Admin</c>, <c>Agent</c>, or <c>Customer</c>.</param>
public sealed record UserProfileResponse(Guid Id, string Email, string DisplayName, string[] Roles);

/// <summary>
/// Request used by a customer or support user to create a ticket.
/// </summary>
/// <param name="Title">Short ticket title used in lists and notifications.</param>
/// <param name="Description">Detailed problem description provided by the requester.</param>
/// <param name="CategoryId">Identifier of an active ticket category.</param>
/// <param name="Priority">Business priority used to select the active SLA policy.</param>
public sealed record CreateTicketRequest(string Title, string Description, Guid CategoryId, TicketPriority Priority);

/// <summary>
/// Request used to move a ticket through the workflow.
/// </summary>
/// <param name="Status">Target workflow status.</param>
/// <param name="Reason">Optional reason shown in the ticket status timeline.</param>
public sealed record UpdateTicketStatusRequest(TicketStatus Status, string? Reason);

/// <summary>
/// Request used by support staff to assign or unassign a ticket.
/// </summary>
/// <param name="AssignedToId">Target support user identifier. Use <see langword="null" /> to clear the assignee.</param>
public sealed record AssignTicketRequest(Guid? AssignedToId);

/// <summary>
/// Request used to add a public comment or an internal support note to a ticket.
/// </summary>
/// <param name="Body">Comment text.</param>
/// <param name="IsInternal">When true, the comment is visible only to support staff.</param>
public sealed record CreateCommentRequest(string Body, bool IsInternal);

/// <summary>
/// Query parameters supported by the ticket list endpoint.
/// </summary>
/// <param name="Status">Optional workflow status filter.</param>
/// <param name="Priority">Optional priority filter.</param>
/// <param name="CategoryId">Optional category filter.</param>
/// <param name="AssignedToId">Optional assignee filter.</param>
/// <param name="Search">Optional text search over ticket title, description, and number.</param>
/// <param name="Mine">When true, support users receive only tickets assigned to them.</param>
/// <param name="Unassigned">When true, support users receive only tickets without an assignee.</param>
/// <param name="Overdue">When true, returns tickets that are currently overdue by first response or resolution SLA.</param>
public sealed record TicketQuery(
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? CategoryId,
    Guid? AssignedToId,
    string? Search,
    bool? Mine,
    bool? Unassigned,
    bool? Overdue);

/// <summary>
/// Request used by administrators to create or update a ticket category.
/// </summary>
/// <param name="Name">Unique category name.</param>
/// <param name="Description">Optional category description for administrators and support staff.</param>
/// <param name="IsActive">Whether the category can be used for new tickets.</param>
public sealed record UpsertCategoryRequest(string Name, string? Description, bool IsActive);

/// <summary>
/// Request used by administrators to create or update an SLA policy.
/// </summary>
/// <param name="Name">Policy name shown in administration screens.</param>
/// <param name="Priority">Ticket priority covered by this policy.</param>
/// <param name="FirstResponseMinutes">Allowed time to the first public support response, in minutes.</param>
/// <param name="ResolutionMinutes">Allowed time to ticket resolution, in minutes.</param>
/// <param name="IsActive">Whether this policy should be selected for newly created tickets.</param>
public sealed record UpsertSlaPolicyRequest(
    string Name,
    TicketPriority Priority,
    int FirstResponseMinutes,
    int ResolutionMinutes,
    bool IsActive);

/// <summary>
/// Request used by administrators to update a user's administrative state.
/// </summary>
/// <param name="DisplayName">Human-readable display name shown in support workflows.</param>
/// <param name="IsActive">Whether the user can authenticate and use protected endpoints.</param>
/// <param name="Roles">Role names assigned to the user.</param>
public sealed record UpdateAdminUserRequest(string DisplayName, bool IsActive, string[] Roles);

/// <summary>
/// Request used to create or update a knowledge base category.
/// </summary>
/// <param name="Name">Category name.</param>
/// <param name="Description">Optional category description.</param>
public sealed record UpsertKnowledgeBaseCategoryRequest(string Name, string? Description);

/// <summary>
/// Request used to create or update a knowledge base article.
/// </summary>
/// <param name="CategoryId">Knowledge base category identifier.</param>
/// <param name="Title">Article title shown in search results.</param>
/// <param name="Slug">URL-friendly unique article slug.</param>
/// <param name="Body">Article body.</param>
/// <param name="IsPublished">Whether customers can see the article.</param>
public sealed record UpsertKnowledgeBaseArticleRequest(Guid CategoryId, string Title, string Slug, string Body, bool IsPublished);
