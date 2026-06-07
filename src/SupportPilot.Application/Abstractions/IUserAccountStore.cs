using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application port for account persistence used by authentication use cases.
/// </summary>
public interface IUserAccountStore
{
    /// <summary>Checks whether an email is already registered.</summary>
    /// <param name="email">Normalized email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a user with this email exists.</returns>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Loads an application role by name.</summary>
    /// <param name="roleName">Role name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Role entity.</returns>
    Task<Role> GetRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>Loads an active user by email with role assignments.</summary>
    /// <param name="email">Normalized email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User entity or null.</returns>
    Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Loads a user by identifier with role assignments.</summary>
    /// <param name="id">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User entity or null.</returns>
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new user to the current unit of work.</summary>
    /// <param name="user">User entity.</param>
    void AddUser(User user);

    /// <summary>Adds an audit log event to the current unit of work.</summary>
    /// <param name="auditLog">Audit log entity.</param>
    void AddAuditLog(AuditLog auditLog);

    /// <summary>Persists pending account and audit changes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of affected records.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
