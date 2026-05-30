using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Common;
using SupportPilot.Contracts;
using SupportPilot.Domain;

namespace SupportPilot.Application.Auth;

public sealed class AuthUseCases(
    IUserAccountStore users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    public async Task<ApplicationResult<UserProfileResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return ApplicationResult<UserProfileResponse>.Failure(
                ApplicationError.Conflict,
                "Пользователь с таким email уже существует.");
        }

        var customerRole = await users.GetRoleAsync("Customer", cancellationToken);
        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.UserRoles.Add(new UserRole { User = user, Role = customerRole });

        users.AddUser(user);
        users.AddAuditLog(new AuditLog
        {
            ActorId = user.Id,
            Action = AuditAction.Created,
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Details = $"Registered user {email}"
        });

        await users.SaveChangesAsync(cancellationToken);

        return ApplicationResult<UserProfileResponse>.Success(ToProfile(user));
    }

    public async Task<ApplicationResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await users.GetActiveUserByEmailAsync(email, cancellationToken);
        if (user is null || !passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password))
        {
            return ApplicationResult<AuthResponse>.Failure(
                ApplicationError.Unauthorized,
                "Неверный email или пароль.");
        }

        return ApplicationResult<AuthResponse>.Success(new AuthResponse(tokenService.CreateToken(user), ToProfile(user)));
    }

    public async Task<ApplicationResult<UserProfileResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ApplicationResult<UserProfileResponse>.Failure(
                ApplicationError.Unauthorized,
                "Пользователь не аутентифицирован.");
        }

        var user = await users.GetUserByIdAsync(userId, cancellationToken);
        return user is null
            ? ApplicationResult<UserProfileResponse>.Failure(ApplicationError.Unauthorized, "Пользователь не найден.")
            : ApplicationResult<UserProfileResponse>.Success(ToProfile(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static UserProfileResponse ToProfile(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.UserRoles.Select(x => x.Role.Name).Order().ToArray());
}
