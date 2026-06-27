namespace iM1os.Application.TenantIdentity;

public interface ITenantProfileService
{
    Task<TenantProfile?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task UpdateAsync(Guid organizationId, Guid userId, UpdateTenantProfileRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<bool> ChangePasswordAsync(Guid organizationId, Guid userId, ChangePasswordRequest request, string? ipAddress, CancellationToken cancellationToken);
}
