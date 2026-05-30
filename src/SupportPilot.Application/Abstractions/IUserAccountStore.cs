using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

public interface IUserAccountStore
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<Role> GetRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void AddUser(User user);
    void AddAuditLog(AuditLog auditLog);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
