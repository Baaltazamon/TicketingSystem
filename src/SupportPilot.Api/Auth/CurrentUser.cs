using System.Security.Claims;

namespace SupportPilot.Api.Auth;

/// <summary>
/// Provides a small typed facade over the authenticated HTTP user.
/// </summary>
/// <param name="httpContextAccessor">Accessor used to read the current request principal.</param>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Gets the authenticated user identifier from the JWT subject claim.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="Guid.Empty"/> when the request is anonymous or the claim is missing/invalid.
    /// </remarks>
    public Guid Id
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current HTTP principal is authenticated.
    /// </summary>
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Determines whether the current user belongs to the specified role.
    /// </summary>
    /// <param name="role">The role name to check, for example <c>Admin</c> or <c>Agent</c>.</param>
    /// <returns><c>true</c> when the current principal has the requested role; otherwise, <c>false</c>.</returns>
    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}
