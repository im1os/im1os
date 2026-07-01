namespace iM1os.Application.Platform;

public interface ITenantModuleEntitlementService
{
    Task<IReadOnlySet<string>> GetEnabledModuleKeysAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetEnabledSupplierConnectorCodesAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<bool> IsSupplierConnectorEnabledAsync(Guid organizationId, string supplierCode, CancellationToken cancellationToken);
}
