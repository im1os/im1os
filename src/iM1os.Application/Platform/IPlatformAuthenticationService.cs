namespace iM1os.Application.Platform;

public interface IPlatformAuthenticationService
{
    Task<PlatformLoginResult?> LoginAsync(PlatformLoginRequest request, CancellationToken cancellationToken);
}
