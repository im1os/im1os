using iM1os.Application.Common;
using iM1os.Application.Platform;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class TenantManagerService(IApplicationDbContext dbContext) : ITenantManagerService
{
    public async Task<PlatformDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.PlatformTenants.AsNoTracking().ToListAsync(cancellationToken);
        var latestEvents = await dbContext.PlatformEvents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(8)
            .Select(x => $"{x.OccurredAtUtc:u} - {x.EventType}")
            .ToListAsync(cancellationToken);

        var recentProvisioning = latestEvents
            .Where(x => x.Contains("TenantProvision", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToArray();

        return new PlatformDashboardSummary(
            tenants.Count,
            tenants.Count(x => x.Status == "Trial"),
            tenants.Count(x => x.Status == "Active"),
            tenants.Count(x => x.Status == "Suspended"),
            0m,
            recentProvisioning,
            latestEvents);
    }

    public async Task<IReadOnlyCollection<TenantManagerRow>> SearchTenantsAsync(string? query, string? status, CancellationToken cancellationToken)
    {
        var tenants = dbContext.PlatformTenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToUpperInvariant();
            tenants = tenants.Where(x => x.OrganizationName.ToUpper().Contains(normalized) || x.Slug.ToUpper().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            tenants = tenants.Where(x => x.Status == status.Trim());
        }

        return await tenants
            .OrderBy(x => x.OrganizationName)
            .Select(x => new TenantManagerRow(
                x.OrganizationId,
                x.OrganizationName,
                x.Status,
                x.SubscriptionPlan,
                x.CreatedAtUtc,
                x.CurrentVersion,
                x.HealthStatus,
                x.ActiveUsers,
                x.Locations,
                x.TrialExpiresAtUtc,
                x.BillingStatus))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantManagerDetail?> GetTenantDetailAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.PlatformTenants
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new TenantManagerRow(
                x.OrganizationId,
                x.OrganizationName,
                x.Status,
                x.SubscriptionPlan,
                x.CreatedAtUtc,
                x.CurrentVersion,
                x.HealthStatus,
                x.ActiveUsers,
                x.Locations,
                x.TrialExpiresAtUtc,
                x.BillingStatus))
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        var modules = await dbContext.TenantModuleEntitlements
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsEnabled)
            .OrderBy(x => x.ModuleKey)
            .Select(x => x.ModuleKey)
            .ToListAsync(cancellationToken);

        var flags = await dbContext.FeatureFlags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Key)
            .Select(x => x.Key)
            .ToListAsync(cancellationToken);

        var history = await dbContext.PlatformEvents
            .AsNoTracking()
            .Where(x => x.TargetOrganizationId == organizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(10)
            .Select(x => $"{x.OccurredAtUtc:u} - {x.EventType}")
            .ToListAsync(cancellationToken);

        var audit = await dbContext.PlatformAuditEvents
            .AsNoTracking()
            .Where(x => x.TargetOrganizationId == organizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(10)
            .Select(x => $"{x.OccurredAtUtc:u} - {x.Action}")
            .ToListAsync(cancellationToken);

        var emails = await dbContext.WelcomeEmails
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => $"{x.CreatedAtUtc:u} - {x.RecipientEmail} - {(x.SentAtUtc.HasValue ? "Sent" : "Pending")}")
            .ToListAsync(cancellationToken);

        return new TenantManagerDetail(tenant, modules, flags, history, audit, emails);
    }
}
