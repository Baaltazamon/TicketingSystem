using Microsoft.AspNetCore.Identity;
using SupportPilot.Application.Abstractions;
using SupportPilot.Domain;

namespace SupportPilot.Infrastructure.Auth;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password) =>
        _hasher.HashPassword(user, password);

    public bool VerifyPassword(User user, string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
}
