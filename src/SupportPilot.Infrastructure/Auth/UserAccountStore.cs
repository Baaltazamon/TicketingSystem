using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Auth;

public sealed class UserAccountStore(SupportPilotDbContext db) : IUserAccountStore
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(x => x.Email == email, cancellationToken);

    public Task<Role> GetRoleAsync(string roleName, CancellationToken cancellationToken = default) =>
        db.Roles.SingleAsync(x => x.Name == roleName, cancellationToken);

    public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void AddUser(User user) => db.Users.Add(user);

    public void AddAuditLog(AuditLog auditLog) => db.AuditLogs.Add(auditLog);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
