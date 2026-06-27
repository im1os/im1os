namespace iM1os.Application.Platform;

public interface ITenantManagerService
{
    Task<PlatformDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TenantManagerRow>> SearchTenantsAsync(string? query, string? status, CancellationToken cancellationToken);

    Task<TenantManagerDetail?> GetTenantDetailAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<TenantManagerDetail?> UpdateTenantAsync(UpdateTenantManagementRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<bool> ResendOwnerInvitationAsync(Guid organizationId, string? platformUserId, CancellationToken cancellationToken);
}
