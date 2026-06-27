namespace iM1os.Application.TenantIdentity;

public interface ITenantIdentityService
{
    Task<TenantLoginResult?> LoginAsync(TenantLoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<TenantLoginResult?> ActivateOwnerAsync(ActivateOwnerRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task RequestPasswordResetAsync(PasswordResetRequestDto request, string? ipAddress, CancellationToken cancellationToken);

    Task<bool> CompletePasswordResetAsync(CompletePasswordResetRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task LogoutAsync(Guid organizationId, Guid userId, string? ipAddress, CancellationToken cancellationToken);
}
