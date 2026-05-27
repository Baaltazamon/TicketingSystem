using SupportPilot.Domain.Domain;

namespace SupportPilot.Contracts.Contracts;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string Token, UserProfileResponse User);
public sealed record UserProfileResponse(Guid Id, string Email, string DisplayName, string[] Roles);

public sealed record CreateTicketRequest(string Title, string Description, Guid CategoryId, TicketPriority Priority);
public sealed record UpdateTicketStatusRequest(TicketStatus Status, string? Reason);
public sealed record AssignTicketRequest(Guid? AssignedToId);
public sealed record CreateCommentRequest(string Body, bool IsInternal);
public sealed record TicketQuery(TicketStatus? Status, TicketPriority? Priority, Guid? CategoryId, Guid? AssignedToId, string? Search);

public sealed record UpsertCategoryRequest(string Name, string? Description, bool IsActive);
public sealed record UpsertSlaPolicyRequest(string Name, TicketPriority Priority, int FirstResponseMinutes, int ResolutionMinutes, bool IsActive);

public sealed record UpsertKnowledgeBaseCategoryRequest(string Name, string? Description);
public sealed record UpsertKnowledgeBaseArticleRequest(Guid CategoryId, string Title, string Slug, string Body, bool IsPublished);
