using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Common;
using SupportPilot.Contracts;
using SupportPilot.Domain;

namespace SupportPilot.Application.Admin;

/// <summary>
/// Provides administrative use cases for users, roles, ticket categories and SLA policies.
/// </summary>
public sealed class AdminUseCases(ISupportPilotDbContext db, IApplicationCache cache)
{
    /// <summary>
    /// Lists all users with their assigned roles and administrative state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Users sorted by email.</returns>
    public async Task<IReadOnlyList<AdminUserResponse>> ListUsersAsync(CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.Email)
            .Select(x => ToAdminUser(x))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lists available role names.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Role names sorted alphabetically.</returns>
    public async Task<IReadOnlyList<string>> ListRolesAsync(CancellationToken cancellationToken)
    {
        return await db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Updates an administrative user profile, active flag and assigned roles.
    /// </summary>
    /// <param name="id">User identifier.</param>
    /// <param name="request">Requested user state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user profile, or an application error.</returns>
    public async Task<ApplicationResult<AdminUserResponse>> UpdateUserAsync(
        Guid id,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return ApplicationResult<AdminUserResponse>.Failure(ApplicationError.NotFound, "User not found.");
        }

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ApplicationResult<AdminUserResponse>.Failure(ApplicationError.Validation, "Display name is required.");
        }

        var requestedRoleNames = request.Roles
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requestedRoleNames.Length == 0)
        {
            return ApplicationResult<AdminUserResponse>.Failure(ApplicationError.Validation, "At least one role is required.");
        }

        var allRoles = await db.Roles.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var roles = allRoles
            .Where(x => requestedRoleNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var unknownRoles = requestedRoleNames
            .Except(roles.Select(x => x.Name), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknownRoles.Length > 0)
        {
            return ApplicationResult<AdminUserResponse>.Failure(
                ApplicationError.Validation,
                $"Unknown roles: {string.Join(", ", unknownRoles)}.");
        }

        var userIsActiveAdmin = user.IsActive && user.UserRoles.Any(x => x.Role.Name == "Admin");
        var userWillRemainActiveAdmin = request.IsActive && roles.Any(x => x.Name == "Admin");
        if (userIsActiveAdmin && !userWillRemainActiveAdmin)
        {
            var activeAdminCount = await db.Users.CountAsync(
                x => x.IsActive && x.UserRoles.Any(role => role.Role.Name == "Admin"),
                cancellationToken);
            if (activeAdminCount <= 1)
            {
                return ApplicationResult<AdminUserResponse>.Failure(
                    ApplicationError.Validation,
                    "Cannot remove or deactivate the last active administrator.");
            }
        }

        user.DisplayName = displayName;
        user.IsActive = request.IsActive;
        user.UserRoles.Clear();
        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        }

        await db.SaveChangesAsync(cancellationToken);

        return ApplicationResult<AdminUserResponse>.Success(ToAdminUser(user));
    }

    /// <summary>
    /// Lists all ticket categories, including inactive categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ticket categories sorted by name.</returns>
    public async Task<IReadOnlyList<TicketCategory>> ListTicketCategoriesAsync(CancellationToken cancellationToken)
    {
        return await db.TicketCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a ticket category.
    /// </summary>
    /// <param name="request">Requested category state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created category, or an application error.</returns>
    public async Task<ApplicationResult<TicketCategory>> CreateTicketCategoryAsync(
        UpsertCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApplicationResult<TicketCategory>.Failure(ApplicationError.Validation, "Category name is required.");
        }

        var category = new TicketCategory
        {
            Name = name,
            Description = request.Description,
            IsActive = request.IsActive
        };

        db.TicketCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);

        return ApplicationResult<TicketCategory>.Success(category);
    }

    /// <summary>
    /// Updates a ticket category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="request">Requested category state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated category, or an application error.</returns>
    public async Task<ApplicationResult<TicketCategory>> UpdateTicketCategoryAsync(
        Guid id,
        UpsertCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await db.TicketCategories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
        {
            return ApplicationResult<TicketCategory>.Failure(ApplicationError.NotFound, "Ticket category not found.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApplicationResult<TicketCategory>.Failure(ApplicationError.Validation, "Category name is required.");
        }

        category.Name = name;
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);

        return ApplicationResult<TicketCategory>.Success(category);
    }

    /// <summary>
    /// Lists all SLA policies.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SLA policies sorted by priority descending.</returns>
    public async Task<IReadOnlyList<SlaPolicy>> ListSlaPoliciesAsync(CancellationToken cancellationToken)
    {
        return await db.SlaPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Updates an SLA policy and invalidates report cache entries.
    /// </summary>
    /// <param name="id">SLA policy identifier.</param>
    /// <param name="request">Requested SLA policy state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated SLA policy, or an application error.</returns>
    public async Task<ApplicationResult<SlaPolicy>> UpdateSlaPolicyAsync(
        Guid id,
        UpsertSlaPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await db.SlaPolicies.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy is null)
        {
            return ApplicationResult<SlaPolicy>.Failure(ApplicationError.NotFound, "SLA policy not found.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApplicationResult<SlaPolicy>.Failure(ApplicationError.Validation, "SLA policy name is required.");
        }

        policy.Name = name;
        policy.Priority = request.Priority;
        policy.FirstResponseMinutes = request.FirstResponseMinutes;
        policy.ResolutionMinutes = request.ResolutionMinutes;
        policy.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateGroupAsync(CacheGroups.Reports, cancellationToken);

        return ApplicationResult<SlaPolicy>.Success(policy);
    }

    private static AdminUserResponse ToAdminUser(User user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.CreatedAt,
            user.UserRoles.Select(x => x.Role.Name).Order().ToArray());
}
