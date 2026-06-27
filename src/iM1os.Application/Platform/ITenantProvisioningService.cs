namespace iM1os.Application.Platform;

public interface ITenantProvisioningService
{
    Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantRequest request, string? platformUserId, CancellationToken cancellationToken);
}
