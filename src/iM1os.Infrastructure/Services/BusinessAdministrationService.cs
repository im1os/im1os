using System.Text.Json;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.Common;
using iM1os.Domain.Audit;
using iM1os.Domain.Identity;
using iM1os.Domain.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class BusinessAdministrationService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IPasswordHasher<ApplicationUser> passwordHasher) : IBusinessAdministrationService
{
    private static readonly ConnectorPlaceholderDto[] DefaultConnectors =
    [
        new("WPS", "Supplier", "Placeholder"),
        new("Parts Unlimited", "Supplier", "Placeholder"),
        new("Turn14", "Supplier", "Placeholder"),
        new("Authorize.net", "Merchant Services", "Placeholder"),
        new("QuickBooks", "Accounting", "Placeholder"),
        new("Twilio", "Notifications", "Placeholder"),
        new("Future Connectors", "Marketplace", "Placeholder")
    ];

    public async Task<BusinessAdministrationWorkspace> GetWorkspaceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var locations = await dbContext.Locations.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new LocationDto(x.Id, x.Name, x.Code, x.Phone, x.AddressLine1, x.City, x.Region, x.TimeZone, x.DefaultLaborRate, x.DefaultTaxRegion, x.Status))
            .ToListAsync(cancellationToken);
        var roles = await dbContext.Roles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new RoleDto(
                x.Name,
                x.IsSystemRole,
                x.RolePermissions.Select(rp => rp.Permission!.Key).OrderBy(key => key).ToList()))
            .ToListAsync(cancellationToken);
        var employeeRows = await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.OrganizationMemberships)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        var locationNames = locations.ToDictionary(x => x.Id, x => x.Name);
        var employees = employeeRows.Select(x =>
        {
            var membership = x.OrganizationMemberships.FirstOrDefault(m => m.OrganizationId == organizationId);
            var role = x.UserRoles.Select(ur => ur.Role?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Unassigned";
            var primaryLocation = membership?.PrimaryLocationId is Guid locationId && locationNames.TryGetValue(locationId, out var name) ? name : null;
            return new EmployeeDto(x.Id, x.DisplayName, x.Email, x.Phone, role, membership?.Status ?? (x.IsActive ? "Active" : "Inactive"), primaryLocation);
        }).ToList();
        var recentActivity = await dbContext.TimelineEvents.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(8)
            .Select(x => $"{x.OccurredAtUtc:u} - {x.Summary}")
            .ToListAsync(cancellationToken);
        var ready = organization.OnboardingCompletedAtUtc is not null && locations.Count > 0 && employees.Count > 0 && config.DefaultLaborRate > 0;

        return new BusinessAdministrationWorkspace(
            organizationId,
            ToProfile(organization),
            ToDto(config),
            locations,
            employees,
            roles,
            DefaultConnectors,
            recentActivity,
            ready ? 100 : CalculateProgress(organization, locations.Count, employees.Count, config),
            ready);
    }

    public async Task UpdateBusinessProfileAsync(Guid organizationId, Guid userId, UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var before = ToProfile(organization);

        organization.Name = request.BusinessName.Trim();
        organization.LegalName = Clean(request.LegalName);
        organization.Dba = Clean(request.Dba);
        organization.LogoUrl = Clean(request.LogoUrl);
        organization.Website = Clean(request.Website);
        organization.Phone = Clean(request.Phone);
        organization.Email = Clean(request.Email);
        organization.TaxId = Clean(request.TaxId);
        organization.AddressLine1 = Clean(request.AddressLine1);
        organization.AddressLine2 = Clean(request.AddressLine2);
        organization.City = Clean(request.City);
        organization.Region = Clean(request.Region);
        organization.PostalCode = Clean(request.PostalCode);
        organization.Country = Clean(request.Country);
        organization.TimeZone = request.TimeZone.Trim();
        organization.Language = request.Language.Trim();
        organization.Currency = request.Currency.Trim();
        organization.DateFormat = request.DateFormat.Trim();
        organization.TimeFormat = request.TimeFormat.Trim();

        await RecordAdminChangeAsync(organizationId, userId, "BusinessProfileUpdated", "Organization", organization.Id.ToString(), before, ToProfile(organization), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertLocationAsync(Guid organizationId, Guid userId, UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var location = request.Id is Guid locationId
            ? await dbContext.Locations.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == organizationId && x.Id == locationId, cancellationToken)
            : null;
        var before = location is null ? null : SnapshotLocation(location);

        if (location is null)
        {
            location = new Location { OrganizationId = organizationId, Name = request.Name.Trim(), Code = request.Code.Trim().ToUpperInvariant() };
            dbContext.Locations.Add(location);
        }

        location.Name = request.Name.Trim();
        location.Code = request.Code.Trim().ToUpperInvariant();
        location.Phone = Clean(request.Phone);
        location.AddressLine1 = Clean(request.AddressLine1);
        location.AddressLine2 = Clean(request.AddressLine2);
        location.City = Clean(request.City);
        location.Region = Clean(request.Region);
        location.PostalCode = Clean(request.PostalCode);
        location.TimeZone = request.TimeZone.Trim();
        location.DefaultLaborRate = request.DefaultLaborRate;
        location.DefaultTaxRegion = Clean(request.DefaultTaxRegion);
        location.Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim();
        location.IsActive = location.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

        await RecordAdminChangeAsync(organizationId, userId, request.Id is null ? "LocationCreated" : "LocationUpdated", "Location", location.Id.ToString(), before, SnapshotLocation(location), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InviteEmployeeAsync(Guid organizationId, Guid userId, InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("An employee with this email already exists.");
        }

        var role = await GetOrCreateRoleAsync(organizationId, request.RoleName, cancellationToken);
        var employee = new ApplicationUser
        {
            OrganizationId = organizationId,
            DisplayName = request.Name.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Phone = Clean(request.Phone),
            PasswordHash = "pending-invitation",
            IsActive = false,
            MustChangePassword = true
        };
        employee.PasswordHash = passwordHasher.HashPassword(employee, Guid.NewGuid().ToString("N"));
        employee.UserRoles.Add(new UserRole { UserId = employee.Id, RoleId = role.Id });
        employee.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = employee.Id,
            DisplayName = employee.DisplayName,
            PrimaryLocationId = request.PrimaryLocationId,
            Status = "Invited",
            IsActive = false
        });
        dbContext.Users.Add(employee);

        await RecordAdminChangeAsync(organizationId, userId, "EmployeeInvited", "ApplicationUser", employee.Id.ToString(), null, new { employee.DisplayName, employee.Email, Role = role.Name }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveLaborConfigurationAsync(Guid organizationId, Guid userId, LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        config.DefaultLaborRate = request.DefaultLaborRate;
        config.DiagnosticRate = request.DiagnosticRate;
        config.EmergencyRate = request.EmergencyRate;
        config.WeekendRate = request.WeekendRate;
        config.EnvironmentalFee = request.EnvironmentalFee;
        config.ShopSuppliesPercent = request.ShopSuppliesPercent;
        await RecordAdminChangeAsync(organizationId, userId, "LaborConfigurationUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTaxConfigurationAsync(Guid organizationId, Guid userId, TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        config.DefaultTaxRate = request.DefaultTaxRate;
        config.RegionalTaxOverridesJson = string.IsNullOrWhiteSpace(request.RegionalOverridesJson) ? "[]" : request.RegionalOverridesJson.Trim();
        await RecordAdminChangeAsync(organizationId, userId, "TaxConfigurationUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveNotificationPreferencesAsync(Guid organizationId, Guid userId, NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        config.NotificationPreferencesJson = JsonSerializer.Serialize(request);
        await RecordAdminChangeAsync(organizationId, userId, "NotificationPreferencesUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var isOwner = await dbContext.UserRoles.IgnoreQueryFilters()
            .AnyAsync(x => x.UserId == userId && x.Role != null && x.Role.OrganizationId == organizationId && x.Role.NormalizedName == "OWNER", cancellationToken);
        if (isOwner)
        {
            return;
        }

        var isPlatformAdministrator = await dbContext.PlatformUsers
            .AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "Platform Administrator", cancellationToken);
        if (isPlatformAdministrator)
        {
            return;
        }

        throw new UnauthorizedAccessException("Only organization owners or platform administrators can manage business administration.");
    }

    private async Task<Role> GetOrCreateRoleAsync(Guid organizationId, string roleName, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(roleName) ? "EMPLOYEE" : roleName.Trim().ToUpperInvariant();
        var role = await dbContext.Roles.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.NormalizedName == normalized, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role { OrganizationId = organizationId, Name = ToTitleCase(normalized), NormalizedName = normalized };
        dbContext.Roles.Add(role);
        return role;
    }

    private async Task<BusinessConfiguration> GetOrCreateConfigurationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var config = await dbContext.BusinessConfigurations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (config is not null)
        {
            return config;
        }

        config = new BusinessConfiguration
        {
            OrganizationId = organizationId,
            DepartmentsJson = JsonSerializer.Serialize(new[] { "Service", "Parts", "Accounting", "Administration" }),
            ConnectorPlaceholdersJson = JsonSerializer.Serialize(DefaultConnectors)
        };
        dbContext.BusinessConfigurations.Add(config);
        return config;
    }

    private async Task RecordAdminChangeAsync(Guid organizationId, Guid userId, string action, string entityName, string entityId, object? before, object? after, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var changes = JsonSerializer.Serialize(new { before, after });
        dbContext.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = userId.ToString(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            ChangesJson = changes,
            OccurredAtUtc = now
        });
        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            EntityType = entityName,
            EntityId = entityId,
            EventType = action,
            ActorUserId = userId.ToString(),
            OccurredAtUtc = now,
            Summary = action,
            PayloadJson = changes
        });
        await Task.CompletedTask;
    }

    private static BusinessProfileDto ToProfile(Organization organization) => new(
        organization.Name,
        organization.LegalName,
        organization.Dba,
        organization.LogoUrl,
        organization.Website,
        organization.Phone,
        organization.Email,
        organization.TaxId,
        organization.AddressLine1,
        organization.AddressLine2,
        organization.City,
        organization.Region,
        organization.PostalCode,
        organization.Country,
        organization.TimeZone,
        organization.Language,
        organization.Currency,
        organization.DateFormat,
        organization.TimeFormat);

    private static BusinessConfigurationDto ToDto(BusinessConfiguration config) => new(
        config.DefaultLaborRate,
        config.DiagnosticRate,
        config.EmergencyRate,
        config.WeekendRate,
        config.EnvironmentalFee,
        config.ShopSuppliesPercent,
        config.DefaultTaxRate,
        config.RegionalTaxOverridesJson,
        config.NumberSequencesJson,
        config.NotificationPreferencesJson,
        config.DepartmentsJson);

    private static object SnapshotLocation(Location location) => new
    {
        location.Name,
        location.Code,
        location.Phone,
        location.AddressLine1,
        location.AddressLine2,
        location.City,
        location.Region,
        location.PostalCode,
        location.TimeZone,
        location.DefaultLaborRate,
        location.DefaultTaxRegion,
        location.Status
    };

    private static int CalculateProgress(Organization organization, int locationCount, int employeeCount, BusinessConfiguration config)
    {
        var progress = organization.OnboardingCompletedAtUtc is null ? 20 : 40;
        if (locationCount > 0) progress += 20;
        if (employeeCount > 0) progress += 15;
        if (config.DefaultLaborRate > 0) progress += 15;
        if (config.DefaultTaxRate > 0) progress += 10;
        return Math.Min(progress, 95);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToTitleCase(string normalized) => string.Join(' ', normalized.Split('_').Select(word => word[..1] + word[1..].ToLowerInvariant()));
}
