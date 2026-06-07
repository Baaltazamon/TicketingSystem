using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application port for creating authentication tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed access token for an authenticated user.
    /// </summary>
    /// <param name="user">Authenticated user with loaded role assignments.</param>
    /// <returns>Serialized access token.</returns>
    string CreateToken(User user);
}
