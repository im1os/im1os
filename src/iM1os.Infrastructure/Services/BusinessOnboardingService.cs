using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.TenantIdentity;
using iM1os.Domain.Audit;
using iM1os.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class BusinessOnboardingService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IBusinessOnboardingService
{
    public async Task<BusinessOnboardingRequest?> GetDraftAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var settings = await dbContext.ApplicationSettings
            .IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
        var location = await dbContext.Locations.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        return new BusinessOnboardingRequest(
            organization.Name,
            settings.GetValueOrDefault("tenant.businessEmail", string.Empty),
            location?.Phone ?? string.Empty,
            settings.GetValueOrDefault("tenant.timezone", "America/Chicago"),
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "Closed",
            "Closed",
            0m,
            true,
            true,
            true);
    }

    public async Task CompleteAsync(Guid organizationId, Guid userId, BusinessOnboardingRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var onboarding = await dbContext.BusinessOnboardings.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (onboarding is null)
        {
            onboarding = new BusinessOnboarding { OrganizationId = organizationId };
            dbContext.BusinessOnboardings.Add(onboarding);
        }

        organization.Name = request.BusinessName.Trim();
        organization.Email = request.BusinessEmail.Trim();
        organization.Phone = request.Phone.Trim();
        organization.TimeZone = request.TimeZone.Trim();
        organization.OnboardingCompletedAtUtc = now;
        onboarding.BusinessName = request.BusinessName.Trim();
        onboarding.BusinessEmail = request.BusinessEmail.Trim();
        onboarding.Phone = request.Phone.Trim();
        onboarding.TimeZone = request.TimeZone.Trim();
        onboarding.BusinessHoursJson = JsonSerializer.Serialize(new
        {
            Monday = request.MondayHours,
            Tuesday = request.TuesdayHours,
            Wednesday = request.WednesdayHours,
            Thursday = request.ThursdayHours,
            Friday = request.FridayHours,
            Saturday = request.SaturdayHours,
            Sunday = request.SundayHours
        });
        onboarding.LaborRate = request.LaborRate;
        onboarding.SuppliersSkipped = request.ConnectSuppliersLater;
        onboarding.MerchantServicesSkipped = request.ConnectMerchantServicesLater;
        onboarding.CompletedSteps = 8;
        onboarding.CompletedAtUtc = now;

        var configuration = await dbContext.BusinessConfigurations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (configuration is null)
        {
            configuration = new BusinessConfiguration { OrganizationId = organizationId };
            dbContext.BusinessConfigurations.Add(configuration);
        }

        configuration.DefaultLaborRate = request.LaborRate;
        configuration.DiagnosticRate = request.LaborRate;
        configuration.EmergencyRate = request.LaborRate;
        configuration.WeekendRate = request.LaborRate;
        configuration.DepartmentsJson = JsonSerializer.Serialize(new[] { "Service", "Parts", "Accounting", "Administration" });
        configuration.ConnectorPlaceholdersJson = JsonSerializer.Serialize(new[] { "WPS", "Parts Unlimited", "Turn14", "Authorize.net", "QuickBooks", "Twilio", "Future Connectors" });

        var platformTenant = await dbContext.PlatformTenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (platformTenant is not null)
        {
            platformTenant.OrganizationName = organization.Name;
        }

        dbContext.TenantIdentityEvents.Add(new TenantIdentityEvent
        {
            OrganizationId = organizationId,
            UserId = userId,
            EventType = "BusinessOnboardingCompleted",
            OccurredAtUtc = now,
            IpAddress = ipAddress,
            PayloadJson = JsonSerializer.Serialize(new { organization.Name, request.LaborRate })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BusinessDashboardSummary> GetDashboardAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var tenant = await dbContext.PlatformTenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        var locations = await dbContext.Locations.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == organizationId, cancellationToken);
        var employees = await dbContext.Users.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == organizationId && x.IsActive, cancellationToken);
        var onboarding = await dbContext.BusinessOnboardings.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        var recentActivity = await dbContext.TenantIdentityEvents
            .IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(6)
            .Select(x => $"{x.OccurredAtUtc:u} - {x.EventType}")
            .ToListAsync(cancellationToken);

        return new BusinessDashboardSummary(
            organization.Name,
            tenant?.SubscriptionPlan ?? "Unknown",
            tenant?.Status ?? (organization.IsActive ? "Active" : "Inactive"),
            locations,
            employees,
            onboarding?.CompletedAtUtc is null ? 25 : 100,
            recentActivity);
    }
}
