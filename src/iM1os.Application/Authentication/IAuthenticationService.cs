using iM1os.Application.Common;

namespace iM1os.Application.Authentication;

public interface IAuthenticationService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
}
