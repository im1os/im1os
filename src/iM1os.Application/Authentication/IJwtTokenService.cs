using iM1os.Domain.Identity;

namespace iM1os.Application.Authentication;

public interface IJwtTokenService
{
    AuthResponse CreateToken(ApplicationUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);
}
