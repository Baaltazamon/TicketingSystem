using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

public interface ITokenService
{
    string CreateToken(User user);
}
