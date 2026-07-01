using iM1os.Application.Common;
using iM1os.Application.Platform;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class TenantModuleEntitlementService(IApplicationDbContext dbContext) : ITenantModuleEntitlementService
{
    public async Task<IReadOnlySet<string>> GetEnabledModuleKeysAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var keys = await dbContext.TenantModuleEntitlements
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsEnabled)
            .Select(x => x.ModuleKey)
            .ToListAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<string>> GetEnabledSupplierConnectorCodesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var keys = await GetEnabledModuleKeysAsync(organizationId, cancellationToken);
        return keys
            .Select(TenantModuleCatalog.SupplierCodeFromModuleKey)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsSupplierConnectorEnabledAsync(Guid organizationId, string supplierCode, CancellationToken cancellationToken)
    {
        var moduleKey = TenantModuleCatalog.SupplierConnectorModuleKey(supplierCode);
        return await dbContext.TenantModuleEntitlements
            .AsNoTracking()
            .AnyAsync(x => x.OrganizationId == organizationId && x.ModuleKey == moduleKey && x.IsEnabled, cancellationToken);
    }
}
