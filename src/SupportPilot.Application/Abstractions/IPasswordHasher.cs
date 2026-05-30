using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string passwordHash, string password);
}
