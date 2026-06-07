using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application port for password hashing and verification.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Produces a password hash for the supplied user and plain-text password.
    /// </summary>
    /// <param name="user">User associated with the password.</param>
    /// <param name="password">Plain-text password.</param>
    /// <returns>Provider-specific password hash.</returns>
    string HashPassword(User user, string password);

    /// <summary>
    /// Verifies a plain-text password against a stored password hash.
    /// </summary>
    /// <param name="user">User associated with the password hash.</param>
    /// <param name="passwordHash">Stored password hash.</param>
    /// <param name="password">Plain-text password supplied by the user.</param>
    /// <returns>True when the password is valid.</returns>
    bool VerifyPassword(User user, string passwordHash, string password);
}
