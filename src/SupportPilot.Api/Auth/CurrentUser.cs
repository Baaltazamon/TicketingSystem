using System.Security.Claims;

namespace SupportPilot.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    public Guid Id
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}
