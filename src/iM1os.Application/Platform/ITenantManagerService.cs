namespace iM1os.Application.Platform;

public interface ITenantManagerService
{
    Task<PlatformDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TenantManagerRow>> SearchTenantsAsync(string? query, string? status, CancellationToken cancellationToken);

    Task<TenantManagerDetail?> GetTenantDetailAsync(Guid organizationId, CancellationToken cancellationToken);
}
