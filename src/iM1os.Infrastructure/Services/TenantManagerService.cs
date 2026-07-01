using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class TenantManagerService(
    IApplicationDbContext dbContext,
    IWelcomeEmailSender welcomeEmailSender,
    IDateTimeProvider dateTimeProvider) : ITenantManagerService
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
                x.BillingStatus,
                x.ProvisioningStatus))
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
                x.BillingStatus,
                x.ProvisioningStatus))
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
        var enabledModuleKeys = modules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moduleOptions = TenantModuleCatalog.All
            .Select(x => new TenantModuleOption(
                x.Key,
                x.Label,
                x.Category,
                x.Description,
                enabledModuleKeys.Contains(x.Key)))
            .ToList();

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

        var owner = await GetOwnerAsync(organizationId, cancellationToken);

        return new TenantManagerDetail(
            tenant,
            owner?.DisplayName,
            owner?.Email,
            owner?.EmailVerifiedAtUtc is not null,
            modules,
            moduleOptions,
            flags,
            history,
            audit,
            emails);
    }

    public async Task<TenantManagerDetail?> UpdateTenantAsync(UpdateTenantManagementRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var tenant = await dbContext.PlatformTenants.SingleOrDefaultAsync(x => x.OrganizationId == request.OrganizationId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == request.OrganizationId, cancellationToken);
        var subscription = await dbContext.PlatformSubscriptions.SingleOrDefaultAsync(x => x.OrganizationId == request.OrganizationId, cancellationToken);

        if (tenant is null || organization is null)
        {
            return null;
        }

        var before = new
        {
            tenant.OrganizationName,
            tenant.Status,
            tenant.SubscriptionPlan,
            tenant.CurrentVersion,
            tenant.HealthStatus,
            tenant.BillingStatus,
            tenant.ProvisioningStatus,
            tenant.TrialExpiresAtUtc
        };

        tenant.OrganizationName = Required(request.OrganizationName, "Organization name");
        tenant.Status = Required(request.Status, "Status");
        tenant.SubscriptionPlan = Required(request.SubscriptionPlan, "Subscription plan");
        tenant.CurrentVersion = Required(request.CurrentVersion, "Current version");
        tenant.HealthStatus = Required(request.HealthStatus, "Health status");
        tenant.BillingStatus = Required(request.BillingStatus, "Billing status");
        tenant.ProvisioningStatus = Required(request.ProvisioningStatus, "Provisioning status");
        tenant.TrialExpiresAtUtc = AsUtc(request.TrialExpiresAtUtc);
        organization.Name = tenant.OrganizationName;
        organization.IsActive = !tenant.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase);

        if (subscription is not null)
        {
            subscription.Plan = tenant.SubscriptionPlan;
            subscription.BillingStatus = tenant.BillingStatus;
            subscription.TrialExpiresAtUtc = tenant.TrialExpiresAtUtc;
            subscription.IsTrial = tenant.Status.Equals("Trial", StringComparison.OrdinalIgnoreCase);
        }

        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            TargetOrganizationId = tenant.OrganizationId,
            ActorPlatformUserId = platformUserId,
            Action = "TenantUpdated",
            OccurredAtUtc = now,
            PreviousValuesJson = JsonSerializer.Serialize(before),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                tenant.OrganizationName,
                tenant.Status,
                tenant.SubscriptionPlan,
                tenant.CurrentVersion,
                tenant.HealthStatus,
                tenant.BillingStatus,
                tenant.ProvisioningStatus,
                tenant.TrialExpiresAtUtc
            })
        });
        dbContext.PlatformEvents.Add(new PlatformEvent
        {
            TargetOrganizationId = tenant.OrganizationId,
            ActorPlatformUserId = platformUserId,
            EventType = "TenantUpdated",
            OccurredAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(new { tenant.OrganizationName, tenant.Status, tenant.SubscriptionPlan }),
            CorrelationId = Guid.NewGuid().ToString("N")
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetTenantDetailAsync(request.OrganizationId, cancellationToken);
    }

    public async Task<TenantManagerDetail?> UpdateTenantModulesAsync(UpdateTenantModulesRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var tenantExists = await dbContext.PlatformTenants.AnyAsync(x => x.OrganizationId == request.OrganizationId, cancellationToken);
        if (!tenantExists)
        {
            return null;
        }

        var now = dateTimeProvider.UtcNow;
        var knownModuleKeys = TenantModuleCatalog.All
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enabledModuleKeys = request.EnabledModules
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(knownModuleKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entitlements = await dbContext.TenantModuleEntitlements
            .Where(x => x.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);
        var entitlementsByKey = entitlements.ToDictionary(x => x.ModuleKey, StringComparer.OrdinalIgnoreCase);
        var before = entitlements
            .Where(x => x.IsEnabled)
            .Select(x => x.ModuleKey)
            .OrderBy(x => x)
            .ToArray();

        foreach (var module in TenantModuleCatalog.All)
        {
            var shouldEnable = enabledModuleKeys.Contains(module.Key);
            if (!entitlementsByKey.TryGetValue(module.Key, out var entitlement))
            {
                entitlement = new TenantModuleEntitlement
                {
                    OrganizationId = request.OrganizationId,
                    ModuleKey = module.Key,
                    IsEnabled = shouldEnable,
                    EnabledAtUtc = now,
                    EnabledByPlatformUserId = shouldEnable ? platformUserId : null
                };
                dbContext.TenantModuleEntitlements.Add(entitlement);
                continue;
            }

            if (entitlement.IsEnabled == shouldEnable)
            {
                continue;
            }

            entitlement.IsEnabled = shouldEnable;
            if (shouldEnable)
            {
                entitlement.EnabledAtUtc = now;
                entitlement.EnabledByPlatformUserId = platformUserId;
            }
        }

        var after = enabledModuleKeys.OrderBy(x => x).ToArray();
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            TargetOrganizationId = request.OrganizationId,
            ActorPlatformUserId = platformUserId,
            Action = "TenantModulesUpdated",
            OccurredAtUtc = now,
            PreviousValuesJson = JsonSerializer.Serialize(new { ModulesEnabled = before }),
            NewValuesJson = JsonSerializer.Serialize(new { ModulesEnabled = after })
        });
        dbContext.PlatformEvents.Add(new PlatformEvent
        {
            TargetOrganizationId = request.OrganizationId,
            ActorPlatformUserId = platformUserId,
            EventType = "TenantModulesUpdated",
            OccurredAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(new { ModulesEnabled = after }),
            CorrelationId = Guid.NewGuid().ToString("N")
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetTenantDetailAsync(request.OrganizationId, cancellationToken);
    }

    public async Task<bool> ResendOwnerInvitationAsync(Guid organizationId, string? platformUserId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(organizationId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (owner is null || organization is null)
        {
            return false;
        }

        var now = dateTimeProvider.UtcNow;
        var token = TenantIdentityService.NewToken();
        dbContext.UserInvitations.Add(new UserInvitation
        {
            OrganizationId = organizationId,
            UserId = owner.Id,
            Email = owner.Email,
            TokenHash = TenantIdentityService.HashToken(token),
            ExpiresAtUtc = now.AddDays(7)
        });

        var welcomeEmail = new WelcomeEmail
        {
            OrganizationId = organizationId,
            RecipientEmail = owner.Email,
            RecipientName = owner.DisplayName,
            Subject = "Your IM1OS owner activation link",
            Body = $"Activate your IM1OS owner account for {organization.Name}: /company/activate?token={Uri.EscapeDataString(token)}",
            CreatedAtUtc = now
        };

        await welcomeEmailSender.SendAsync(welcomeEmail.RecipientEmail, welcomeEmail.Subject, welcomeEmail.Body, cancellationToken);
        welcomeEmail.SentAtUtc = now;
        dbContext.WelcomeEmails.Add(welcomeEmail);
        dbContext.PlatformEvents.Add(new PlatformEvent
        {
            TargetOrganizationId = organizationId,
            ActorPlatformUserId = platformUserId,
            EventType = "OwnerInvitationResent",
            OccurredAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(new { owner.Email, owner.DisplayName }),
            CorrelationId = Guid.NewGuid().ToString("N")
        });
        dbContext.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            TargetOrganizationId = organizationId,
            ActorPlatformUserId = platformUserId,
            Action = "OwnerInvitationResent",
            OccurredAtUtc = now,
            NewValuesJson = JsonSerializer.Serialize(new { owner.Email })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<ApplicationUser?> GetOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .IgnoreQueryFilters()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.UserRoles.Any(ur => ur.Role != null && ur.Role.OrganizationId == organizationId && ur.Role.NormalizedName == "OWNER"), cancellationToken);
    }

    private static string Required(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.")
            : value.Trim();
    }

    private static DateTimeOffset? AsUtc(DateTimeOffset? value)
    {
        return value is null
            ? null
            : new DateTimeOffset(value.Value.DateTime, TimeSpan.Zero);
    }
}
