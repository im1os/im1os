using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.Audit;
using iM1os.Domain.Employees;
using iM1os.Domain.GlobalCatalog;
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
    private static readonly JsonSerializerOptions ConnectorJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ConnectorDto[] DefaultConnectors =
    [
        DefaultWpsConnector(),
        new("parts-unlimited", "Parts Unlimited", "Supplier", "Planned", "Supplier catalog, cost, and availability access.", false, "Manual", null, ["Catalog import", "Inventory sync", "Purchase orders"], null),
        new("turn14", "Turn14", "Supplier", "Planned", "Performance parts catalog and availability access.", false, "Manual", null, ["Catalog import", "Inventory sync", "Purchase orders"], null),
        new("authorize-net", "Authorize.net", "Merchant Services", "Planned", "Payment processing connector for card-present and card-not-present workflows.", false, "Manual", null, ["Payment authorization", "Capture", "Refunds"], null),
        new("quickbooks", "QuickBooks", "Accounting", "Planned", "Accounting export connector for invoices, payments, taxes, and deposits.", false, "Manual", null, ["Invoice export", "Payment export", "Tax mapping"], null),
        new("twilio", "Twilio", "Notifications", "Planned", "SMS and voice notification connector for customer and technician messaging.", false, "Manual", null, ["SMS", "Voice", "Delivery status"], null),
        new("future-connectors", "Future Connectors", "Marketplace", "Planned", "Connector marketplace placeholder for additional suppliers and business systems.", false, "Manual", null, ["Marketplace"], null)
    ];
    private static readonly SupplierWarehousePreferenceDto[] DefaultSupplierWarehousePreferences =
    [
        new("WPS", "Western Power Sports", null,
        [
            new("CA", "CA"),
            new("GA", "GA"),
            new("ID", "ID"),
            new("IN", "IN"),
            new("PA", "PA"),
            new("PA2", "PA2"),
            new("TX", "TX")
        ]),
        new("TURN14", "Turn 14 Distribution", null,
        [
            new("01", "Turn14 East"),
            new("02", "Turn14 West"),
            new("03", "Turn14 Midwest"),
            new("59", "Turn14 Central")
        ]),
        new("PU", "Parts Unlimited", null,
        [
            new("NC", "NC"),
            new("NV", "NV"),
            new("NY", "NY"),
            new("TX", "TX"),
            new("WI", "WI")
        ])
    ];

    public static string DefaultConnectorConfigurationJson() => JsonSerializer.Serialize(DefaultConnectors, ConnectorJsonOptions);

    public async Task<BusinessAdministrationWorkspace> GetWorkspaceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
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
        var employeeRows = await dbContext.Employees.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.OrganizationMemberships)
            .Include(x => x.CompensationRecords)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        var locationNames = locations.ToDictionary(x => x.Id, x => x.Name);
        var employees = employeeRows.Select(x =>
        {
            var membership = x.LoginAccount?.OrganizationMemberships.FirstOrDefault(m => m.OrganizationId == organizationId);
            var role = x.LoginAccount?.UserRoles.Select(ur => ur.Role?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "No Login";
            var primaryLocation = membership?.PrimaryLocationId is Guid locationId && locationNames.TryGetValue(locationId, out var name) ? name : null;
            return new EmployeeDto(x.Id, x.DisplayName, x.Email ?? x.LoginAccount?.Email ?? string.Empty, x.Phone, role, x.Status, primaryLocation);
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
            await BuildCompanySupplierConnectorsAsync(organizationId, cancellationToken),
            await BuildTimeClockWorkspaceAsync(organization, employeeRows, cancellationToken),
            recentActivity,
            ready ? 100 : CalculateProgress(organization, locations.Count, employees.Count, config),
            ready);
    }

    public async Task UpdateBusinessProfileAsync(Guid organizationId, Guid userId, UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var before = ToProfile(organization);

        organization.Name = Required(request.BusinessName, "Company name");
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
        organization.TimeZone = Defaulted(request.TimeZone, organization.TimeZone, "America/Chicago");
        organization.Language = Defaulted(request.Language, organization.Language, "en-US");
        organization.Currency = Defaulted(request.Currency, organization.Currency, "USD");
        organization.DateFormat = Defaulted(request.DateFormat, organization.DateFormat, "MM/dd/yyyy");
        organization.TimeFormat = Defaulted(request.TimeFormat, organization.TimeFormat, "h:mm tt");

        await RecordAdminChangeAsync(organizationId, userId, "BusinessProfileUpdated", "Organization", organization.Id.ToString(), before, ToProfile(organization), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertLocationAsync(Guid organizationId, Guid userId, UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var name = Required(request.Name, "Location name");
        var code = Required(request.Code, "Location code").ToUpperInvariant();
        var timeZone = Defaulted(request.TimeZone, null, "America/Chicago");
        var location = request.Id is Guid locationId
            ? await dbContext.Locations.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == organizationId && x.Id == locationId, cancellationToken)
            : null;
        var before = location is null ? null : SnapshotLocation(location);

        if (location is null)
        {
            location = new Location { OrganizationId = organizationId, Name = name, Code = code };
            dbContext.Locations.Add(location);
        }

        location.Name = name;
        location.Code = code;
        location.Phone = Clean(request.Phone);
        location.AddressLine1 = Clean(request.AddressLine1);
        location.AddressLine2 = Clean(request.AddressLine2);
        location.City = Clean(request.City);
        location.Region = Clean(request.Region);
        location.PostalCode = Clean(request.PostalCode);
        location.TimeZone = timeZone;
        location.DefaultLaborRate = request.DefaultLaborRate;
        location.DefaultTaxRegion = Clean(request.DefaultTaxRegion);
        location.Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim();
        location.IsActive = location.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

        await RecordAdminChangeAsync(organizationId, userId, request.Id is null ? "LocationCreated" : "LocationUpdated", "Location", location.Id.ToString(), before, SnapshotLocation(location), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InviteEmployeeAsync(Guid organizationId, Guid userId, InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("An employee with this email already exists.");
        }

        var role = await GetOrCreateRoleAsync(organizationId, request.RoleName, cancellationToken);
        var employee = new Employee
        {
            OrganizationId = organizationId,
            DisplayName = request.Name.Trim(),
            Email = request.Email.Trim(),
            FirstName = FirstName(request.Name),
            LastName = LastName(request.Name),
            Phone = Clean(request.Phone),
            Status = "Active"
        };
        dbContext.Employees.Add(employee);

        var loginAccount = new ApplicationUser
        {
            OrganizationId = organizationId,
            EmployeeId = employee.Id,
            DisplayName = employee.DisplayName,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Phone = Clean(request.Phone),
            PasswordHash = "pending-invitation",
            IsActive = false,
            MustChangePassword = true
        };
        loginAccount.PasswordHash = passwordHasher.HashPassword(loginAccount, Guid.NewGuid().ToString("N"));
        loginAccount.UserRoles.Add(new UserRole { UserId = loginAccount.Id, RoleId = role.Id });
        loginAccount.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = loginAccount.Id,
            DisplayName = employee.DisplayName,
            PrimaryLocationId = request.PrimaryLocationId,
            Status = "Invited",
            IsActive = false
        });
        dbContext.Users.Add(loginAccount);
        employee.LoginAccount = loginAccount;

        await RecordAdminChangeAsync(organizationId, userId, "EmployeeInvited", "Employee", employee.Id.ToString(), null, new { employee.DisplayName, employee.Email, Role = role.Name }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveLaborConfigurationAsync(Guid organizationId, Guid userId, LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        config.DefaultLaborRate = request.DefaultLaborRate;
        config.DiagnosticRate = request.DiagnosticRate;
        config.EmergencyRate = request.EmergencyRate;
        config.WeekendRate = request.WeekendRate;
        config.EnvironmentalFee = request.EnvironmentalFee;
        config.ShopSuppliesPercent = request.ShopSuppliesPercent;
        config.LaborLineItemsTaxable = request.LaborLineItemsTaxable;
        await RecordAdminChangeAsync(organizationId, userId, "LaborConfigurationUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTaxConfigurationAsync(Guid organizationId, Guid userId, TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        config.DefaultTaxRate = request.DefaultTaxRate;
        config.RegionalTaxOverridesJson = string.IsNullOrWhiteSpace(request.RegionalOverridesJson) ? "[]" : request.RegionalOverridesJson.Trim();
        await RecordAdminChangeAsync(organizationId, userId, "TaxConfigurationUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveNotificationPreferencesAsync(Guid organizationId, Guid userId, NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        config.NotificationPreferencesJson = JsonSerializer.Serialize(request);
        await RecordAdminChangeAsync(organizationId, userId, "NotificationPreferencesUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSupplierPreferencesAsync(Guid organizationId, Guid userId, SupplierPreferencesRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var before = ToDto(config);
        var preferredSupplierCode = NormalizeSupplierCode(request.PreferredSupplierCode);
        var warehouseCodes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["WPS"] = Clean(request.WpsPreferredWarehouseCode),
            ["TURN14"] = Clean(request.Turn14PreferredWarehouseCode),
            ["PU"] = Clean(request.PartsUnlimitedPreferredWarehouseCode)
        };
        config.SupplierPreferencesJson = JsonSerializer.Serialize(new StoredSupplierPreferences(
            preferredSupplierCode,
            warehouseCodes
                .Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value!, StringComparer.OrdinalIgnoreCase)), ConnectorJsonOptions);
        await RecordAdminChangeAsync(organizationId, userId, "SupplierPreferencesUpdated", "BusinessConfiguration", config.Id.ToString(), before, ToDto(config), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClockEmployeeAsync(Guid organizationId, Guid userId, ClockEmployeeRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var employee = await dbContext.Employees.IgnoreQueryFilters()
            .Include(x => x.LoginAccount)
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.EmployeeId && x.DeletedAtUtc == null, cancellationToken);
        var user = employee.LoginAccount ?? throw new InvalidOperationException("This employee does not have a login account.");
        var pin = Required(request.Pin, "PIN");
        if (pin.Length != 4 || pin.Any(x => !char.IsDigit(x)) || user.PinHash != HashPin(organizationId, pin))
        {
            throw new InvalidOperationException("PIN did not match this employee.");
        }

        var action = NormalizeClockAction(request.Action);
        var openPunch = await dbContext.EmployeeTimePunches.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.EmployeeId == employee.Id && x.ClockOutUtc == null && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.ClockInUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (action == "In")
        {
            if (openPunch is not null)
            {
                throw new InvalidOperationException("This employee is already clocked in.");
            }

            var punch = new EmployeeTimePunch
            {
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                ClockInUtc = now,
                Source = "PIN"
            };
            dbContext.EmployeeTimePunches.Add(punch);
            await RecordAdminChangeAsync(organizationId, userId, "EmployeeClockedIn", "EmployeeTimePunch", punch.Id.ToString(), null, new { employee.DisplayName, punch.ClockInUtc }, cancellationToken);
        }
        else
        {
            if (openPunch is null)
            {
                throw new InvalidOperationException("This employee is not clocked in.");
            }

            openPunch.ClockOutUtc = now;
            openPunch.Hours = CalculateHours(openPunch.ClockInUtc, openPunch.ClockOutUtc.Value);
            await RecordAdminChangeAsync(organizationId, userId, "EmployeeClockedOut", "EmployeeTimePunch", openPunch.Id.ToString(), null, new { employee.DisplayName, openPunch.ClockInUtc, openPunch.ClockOutUtc, openPunch.Hours }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTimePunchAsync(Guid organizationId, Guid userId, AddTimePunchRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        await EnsureEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        var clockInUtc = ToUtc(request.ClockInLocal, organization.TimeZone);
        DateTimeOffset? clockOutUtc = request.ClockOutLocal is null ? null : ToUtc(request.ClockOutLocal.Value, organization.TimeZone);
        ValidatePunchTimes(clockInUtc, clockOutUtc);

        var punch = new EmployeeTimePunch
        {
            OrganizationId = organizationId,
            EmployeeId = request.EmployeeId,
            ClockInUtc = clockInUtc,
            ClockOutUtc = clockOutUtc,
            Hours = clockOutUtc is null ? null : CalculateHours(clockInUtc, clockOutUtc.Value),
            Note = Clean(request.Note),
            IsManualEntry = true,
            Source = "Manual"
        };
        dbContext.EmployeeTimePunches.Add(punch);
        await RecordAdminChangeAsync(organizationId, userId, "TimePunchAdded", "EmployeeTimePunch", punch.Id.ToString(), null, PunchSnapshot(punch), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTimePunchAsync(Guid organizationId, Guid userId, UpdateTimePunchRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var punch = await dbContext.EmployeeTimePunches.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.PunchId && x.DeletedAtUtc == null, cancellationToken);
        var before = PunchSnapshot(punch);
        var clockInUtc = ToUtc(request.ClockInLocal, organization.TimeZone);
        DateTimeOffset? clockOutUtc = request.ClockOutLocal is null ? null : ToUtc(request.ClockOutLocal.Value, organization.TimeZone);
        ValidatePunchTimes(clockInUtc, clockOutUtc);

        punch.ClockInUtc = clockInUtc;
        punch.ClockOutUtc = clockOutUtc;
        punch.Hours = clockOutUtc is null ? null : CalculateHours(clockInUtc, clockOutUtc.Value);
        punch.Note = Clean(request.Note);
        punch.IsManualEntry = true;
        await RecordAdminChangeAsync(organizationId, userId, "TimePunchUpdated", "EmployeeTimePunch", punch.Id.ToString(), before, PunchSnapshot(punch), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTimePunchAsync(Guid organizationId, Guid userId, DeleteTimePunchRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var punch = await dbContext.EmployeeTimePunches.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.PunchId && x.DeletedAtUtc == null, cancellationToken);
        punch.DeletedAtUtc = dateTimeProvider.UtcNow;
        await RecordAdminChangeAsync(organizationId, userId, "TimePunchDeleted", "EmployeeTimePunch", punch.Id.ToString(), PunchSnapshot(punch), null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddScheduleShiftAsync(Guid organizationId, Guid userId, AddScheduleShiftRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("Shift end time must be after start time.");
        }

        var shift = new EmployeeScheduleShift
        {
            OrganizationId = organizationId,
            EmployeeId = request.EmployeeId,
            ShiftDate = request.ShiftDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Note = Clean(request.Note)
        };
        dbContext.EmployeeScheduleShifts.Add(shift);
        await RecordAdminChangeAsync(organizationId, userId, "ScheduleShiftAdded", "EmployeeScheduleShift", shift.Id.ToString(), null, ShiftSnapshot(shift), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteScheduleShiftAsync(Guid organizationId, Guid userId, DeleteScheduleShiftRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var shift = await dbContext.EmployeeScheduleShifts.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.ShiftId && x.DeletedAtUtc == null, cancellationToken);
        shift.DeletedAtUtc = dateTimeProvider.UtcNow;
        await RecordAdminChangeAsync(organizationId, userId, "ScheduleShiftDeleted", "EmployeeScheduleShift", shift.Id.ToString(), ShiftSnapshot(shift), null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTimeOffAsync(Guid organizationId, Guid userId, AddTimeOffRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        if (request.EndDate < request.StartDate)
        {
            throw new InvalidOperationException("Time off end date must be on or after start date.");
        }

        if (request.HoursPerDay <= 0 || request.HoursPerDay > 24)
        {
            throw new InvalidOperationException("Hours per day must be between 0 and 24.");
        }

        var timeOff = new EmployeeTimeOffRequest
        {
            OrganizationId = organizationId,
            EmployeeId = request.EmployeeId,
            Type = Required(request.Type, "Time off type"),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            HoursPerDay = request.HoursPerDay,
            Note = Clean(request.Note),
            Status = "Pending"
        };
        dbContext.EmployeeTimeOffRequests.Add(timeOff);
        await RecordAdminChangeAsync(organizationId, userId, "TimeOffRequested", "EmployeeTimeOffRequest", timeOff.Id.ToString(), null, TimeOffSnapshot(timeOff), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetTimeOffStatusAsync(Guid organizationId, Guid userId, SetTimeOffStatusRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var timeOff = await dbContext.EmployeeTimeOffRequests.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.TimeOffRequestId && x.DeletedAtUtc == null, cancellationToken);
        var before = TimeOffSnapshot(timeOff);
        timeOff.Status = NormalizeTimeOffStatus(request.Status);
        timeOff.ReviewedAtUtc = dateTimeProvider.UtcNow;
        timeOff.ReviewedByUserId = userId.ToString();
        await RecordAdminChangeAsync(organizationId, userId, "TimeOffStatusUpdated", "EmployeeTimeOffRequest", timeOff.Id.ToString(), before, TimeOffSnapshot(timeOff), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTimeOffAsync(Guid organizationId, Guid userId, DeleteTimeOffRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var timeOff = await dbContext.EmployeeTimeOffRequests.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.TimeOffRequestId && x.DeletedAtUtc == null, cancellationToken);
        timeOff.DeletedAtUtc = dateTimeProvider.UtcNow;
        await RecordAdminChangeAsync(organizationId, userId, "TimeOffDeleted", "EmployeeTimeOffRequest", timeOff.Id.ToString(), TimeOffSnapshot(timeOff), null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCompanyAssetAsync(Guid organizationId, Guid userId, AddCompanyAssetRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        if (request.ReturnedDate is not null && request.IssuedDate is not null && request.ReturnedDate < request.IssuedDate)
        {
            throw new InvalidOperationException("Returned date cannot be before issued date.");
        }

        var asset = new EmployeeCompanyAsset
        {
            OrganizationId = organizationId,
            EmployeeId = request.EmployeeId,
            Name = Required(request.Name, "Asset name"),
            AssetTag = Clean(request.AssetTag),
            SerialNumber = Clean(request.SerialNumber),
            IssuedDate = request.IssuedDate,
            ReturnedDate = request.ReturnedDate,
            Status = request.ReturnedDate is null ? "Issued" : "Returned",
            Note = Clean(request.Note)
        };
        dbContext.EmployeeCompanyAssets.Add(asset);
        await RecordAdminChangeAsync(organizationId, userId, "CompanyAssetIssued", "EmployeeCompanyAsset", asset.Id.ToString(), null, AssetSnapshot(asset), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReturnCompanyAssetAsync(Guid organizationId, Guid userId, ReturnCompanyAssetRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var asset = await dbContext.EmployeeCompanyAssets.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.AssetId && x.DeletedAtUtc == null, cancellationToken);
        if (asset.IssuedDate is not null && request.ReturnedDate < asset.IssuedDate)
        {
            throw new InvalidOperationException("Returned date cannot be before issued date.");
        }

        var before = AssetSnapshot(asset);
        asset.ReturnedDate = request.ReturnedDate;
        asset.Status = "Returned";
        asset.Note = Clean(request.Note) ?? asset.Note;
        await RecordAdminChangeAsync(organizationId, userId, "CompanyAssetReturned", "EmployeeCompanyAsset", asset.Id.ToString(), before, AssetSnapshot(asset), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCompanyAssetAsync(Guid organizationId, Guid userId, DeleteCompanyAssetRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var asset = await dbContext.EmployeeCompanyAssets.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == request.AssetId && x.DeletedAtUtc == null, cancellationToken);
        asset.DeletedAtUtc = dateTimeProvider.UtcNow;
        await RecordAdminChangeAsync(organizationId, userId, "CompanyAssetDeleted", "EmployeeCompanyAsset", asset.Id.ToString(), AssetSnapshot(asset), null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSafetyIncidentAsync(Guid organizationId, Guid userId, AddSafetyIncidentRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        if (request.EmployeeId is Guid employeeId)
        {
            await EnsureEmployeeAsync(organizationId, employeeId, cancellationToken);
        }

        if (request.LostTimeHours < 0)
        {
            throw new InvalidOperationException("Lost time hours cannot be negative.");
        }

        var incident = new EmployeeSafetyIncident
        {
            OrganizationId = organizationId,
            EmployeeId = request.EmployeeId,
            IncidentDate = request.IncidentDate,
            IncidentType = Required(request.IncidentType, "Incident type"),
            Severity = Clean(request.Severity),
            LostTimeHours = request.LostTimeHours,
            IsOshaRecordable = request.IsOshaRecordable,
            ReportedToOsha = request.ReportedToOsha,
            Description = Clean(request.Description)
        };
        dbContext.EmployeeSafetyIncidents.Add(incident);
        await RecordAdminChangeAsync(organizationId, userId, "SafetyIncidentLogged", "EmployeeSafetyIncident", incident.Id.ToString(), null, SafetyIncidentSnapshot(incident), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveWpsConnectorAsync(Guid organizationId, Guid userId, SaveWpsConnectorRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        var config = await GetOrCreateConfigurationAsync(organizationId, cancellationToken);
        var connectors = ReadConnectors(config).ToList();
        var current = connectors.SingleOrDefault(x => x.Key == "wps") ?? DefaultWpsConnector();
        var before = current;
        var credentialStatus = !string.IsNullOrWhiteSpace(request.ApiPassword)
            ? "Configured"
            : current.WpsConfiguration?.CredentialStatus ?? "Missing";
        var status = NormalizeConnectorStatus(request.Status);

        if (status == "Ready" &&
            (string.IsNullOrWhiteSpace(request.DealerNumber) ||
             string.IsNullOrWhiteSpace(request.Username) ||
             credentialStatus != "Configured"))
        {
            throw new InvalidOperationException("WPS requires a dealer number, username, and API password before it can be marked ready.");
        }

        var updated = current with
        {
            Status = status,
            IsEnabled = status == "Ready",
            SyncCadence = NormalizeSyncCadence(request.SyncCadence),
            WpsConfiguration = new WpsConnectorConfigurationDto(
                Clean(request.DealerNumber) ?? string.Empty,
                Clean(request.Username) ?? string.Empty,
                Clean(request.Endpoint) ?? string.Empty,
                Clean(request.PriceCode) ?? string.Empty,
                Clean(request.DefaultWarehouse) ?? string.Empty,
                request.ImportCatalog,
                request.ImportInventory,
                request.SubmitPurchaseOrders,
                request.UseSandbox,
                credentialStatus)
        };

        var existingIndex = connectors.FindIndex(x => x.Key == "wps");
        if (existingIndex >= 0)
        {
            connectors[existingIndex] = updated;
        }
        else
        {
            connectors.Insert(0, updated);
        }

        config.ConnectorPlaceholdersJson = JsonSerializer.Serialize(connectors, ConnectorJsonOptions);
        await RecordAdminChangeAsync(organizationId, userId, "WpsConnectorUpdated", "BusinessConfiguration", config.Id.ToString(), before, updated, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TimeClockWorkspace> BuildTimeClockWorkspaceAsync(Organization organization, IReadOnlyCollection<Employee> employeeRows, CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var today = ToLocalDate(nowUtc, organization.TimeZone);
        var payrollPeriod = CurrentPayrollPeriod(today);
        var employeeLookup = employeeRows.ToDictionary(x => x.Id, x => x.DisplayName);
        var employeeOptions = employeeRows
            .Where(x => x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DisplayName)
            .Select(x => new TimeClockEmployeeOption(x.Id, x.DisplayName, !string.IsNullOrWhiteSpace(x.LoginAccount?.PinHash), x.Status))
            .ToList();

        var openPunchRows = await dbContext.EmployeeTimePunches.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.ClockOutUtc == null)
            .OrderBy(x => x.ClockInUtc)
            .ToListAsync(cancellationToken);
        var openPunches = openPunchRows
            .Where(x => employeeLookup.ContainsKey(x.EmployeeId))
            .Select(x => new TimeClockOpenPunchDto(
                x.Id,
                x.EmployeeId,
                employeeLookup[x.EmployeeId],
                x.ClockInUtc,
                CalculateHours(x.ClockInUtc, nowUtc)))
            .ToList();

        var recentStartUtc = nowUtc.AddDays(-30);
        var recentPunchRows = await dbContext.EmployeeTimePunches.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.ClockInUtc >= recentStartUtc)
            .OrderByDescending(x => x.ClockInUtc)
            .Take(40)
            .ToListAsync(cancellationToken);
        var recentPunches = recentPunchRows
            .Where(x => employeeLookup.ContainsKey(x.EmployeeId))
            .Select(x => ToPunchDto(x, employeeLookup[x.EmployeeId], nowUtc))
            .ToList();

        var scheduleEnd = today.AddDays(21);
        var shiftRows = await dbContext.EmployeeScheduleShifts.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.ShiftDate >= today && x.ShiftDate <= scheduleEnd)
            .OrderBy(x => x.ShiftDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);
        var shifts = shiftRows
            .Where(x => employeeLookup.ContainsKey(x.EmployeeId))
            .Select(x => ToShiftDto(x, employeeLookup[x.EmployeeId]))
            .ToList();

        var timeOffStart = today.AddDays(-30);
        var timeOffRows = await dbContext.EmployeeTimeOffRequests.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.EndDate >= timeOffStart)
            .OrderByDescending(x => x.StartDate)
            .Take(40)
            .ToListAsync(cancellationToken);
        var timeOff = timeOffRows
            .Where(x => employeeLookup.ContainsKey(x.EmployeeId))
            .Select(x => ToTimeOffDto(x, employeeLookup[x.EmployeeId]))
            .ToList();

        var assetRows = await dbContext.EmployeeCompanyAssets.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null)
            .OrderBy(x => x.ReturnedDate == null ? 0 : 1)
            .ThenByDescending(x => x.IssuedDate)
            .Take(80)
            .ToListAsync(cancellationToken);
        var assets = assetRows
            .Where(x => employeeLookup.ContainsKey(x.EmployeeId))
            .Select(x => ToAssetDto(x, employeeLookup[x.EmployeeId]))
            .ToList();

        var safetyStart = today.AddMonths(-12);
        var incidentRows = await dbContext.EmployeeSafetyIncidents.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.IncidentDate >= safetyStart)
            .OrderByDescending(x => x.IncidentDate)
            .Take(80)
            .ToListAsync(cancellationToken);
        var incidents = incidentRows
            .Select(x => ToSafetyIncidentDto(x, x.EmployeeId is Guid employeeId && employeeLookup.TryGetValue(employeeId, out var name) ? name : "Company-wide"))
            .ToList();
        var safetySummary = new SafetySummaryDto(
            incidents.Count,
            incidents.Count(x => x.IsOshaRecordable),
            incidents.Count(x => x.ReportedToOsha),
            Math.Round(incidents.Sum(x => x.LostTimeHours), 2));

        var payrollStartUtc = StartOfLocalDateUtc(payrollPeriod.Start, organization.TimeZone);
        var payrollEndUtc = StartOfLocalDateUtc(payrollPeriod.End.AddDays(1), organization.TimeZone);
        var payrollPunches = await dbContext.EmployeeTimePunches.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.ClockOutUtc != null && x.ClockInUtc >= payrollStartUtc && x.ClockInUtc < payrollEndUtc)
            .ToListAsync(cancellationToken);
        var payrollShifts = await dbContext.EmployeeScheduleShifts.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.ShiftDate >= payrollPeriod.Start && x.ShiftDate <= payrollPeriod.End)
            .ToListAsync(cancellationToken);
        var payrollTimeOff = await dbContext.EmployeeTimeOffRequests.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organization.Id && x.DeletedAtUtc == null && x.Status == "Approved" && x.EndDate >= payrollPeriod.Start && x.StartDate <= payrollPeriod.End)
            .ToListAsync(cancellationToken);
        var payroll = BuildPayrollSummary(employeeRows, payrollPunches, payrollShifts, payrollTimeOff, payrollPeriod.Start, payrollPeriod.End);

        return new TimeClockWorkspace(employeeOptions, openPunches, recentPunches, shifts, timeOff, assets, incidents, safetySummary, payroll, payrollPeriod.Start, payrollPeriod.End);
    }

    private static TimeClockPayrollSummary BuildPayrollSummary(
        IReadOnlyCollection<Employee> employees,
        IReadOnlyCollection<EmployeeTimePunch> punches,
        IReadOnlyCollection<EmployeeScheduleShift> shifts,
        IReadOnlyCollection<EmployeeTimeOffRequest> timeOff,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var activeEmployeeIds = employees
            .Where(x => x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToHashSet();
        var employeeNames = employees.ToDictionary(x => x.Id, x => x.DisplayName);
        var employeeIds = punches.Select(x => x.EmployeeId)
            .Concat(shifts.Select(x => x.EmployeeId))
            .Concat(timeOff.Select(x => x.EmployeeId))
            .Concat(activeEmployeeIds)
            .Distinct()
            .Where(employeeNames.ContainsKey)
            .OrderBy(id => employeeNames[id])
            .ToList();
        var rows = employeeIds.Select(employeeId =>
        {
            var employee = employees.Single(x => x.Id == employeeId);
            var compensation = EffectiveHourlyCompensation(employee, periodEnd);
            var hourlyRate = compensation?.HourlyRate;
            var payrollType = compensation?.PayrollType ?? "Hourly";
            var workedHours = punches
                .Where(x => x.EmployeeId == employeeId)
                .Sum(x => x.Hours ?? (x.ClockOutUtc is DateTimeOffset outUtc ? CalculateHours(x.ClockInUtc, outUtc) : 0m));
            var scheduledHours = shifts
                .Where(x => x.EmployeeId == employeeId)
                .Sum(ShiftHours);
            var timeOffHours = timeOff
                .Where(x => x.EmployeeId == employeeId)
                .Sum(x => OverlapDays(x.StartDate, x.EndDate, periodStart, periodEnd) * x.HoursPerDay);
            var paidTimeOffHours = timeOff
                .Where(x => x.EmployeeId == employeeId && IsPaidTimeOffType(x.Type))
                .Sum(x => OverlapDays(x.StartDate, x.EndDate, periodStart, periodEnd) * x.HoursPerDay);
            var paidHours = workedHours + paidTimeOffHours;
            var grossPay = hourlyRate is decimal rate ? paidHours * rate : 0m;
            return new TimeClockPayrollEmployeeSummary(
                employeeId,
                employeeNames[employeeId],
                payrollType,
                hourlyRate,
                Math.Round(workedHours, 2),
                Math.Round(scheduledHours, 2),
                Math.Round(timeOffHours, 2),
                Math.Round(paidTimeOffHours, 2),
                Math.Round(paidHours, 2),
                Math.Round(workedHours + timeOffHours - scheduledHours, 2),
                Math.Round(grossPay, 2));
        }).ToList();

        return new TimeClockPayrollSummary(
            rows,
            Math.Round(rows.Sum(x => x.WorkedHours), 2),
            Math.Round(rows.Sum(x => x.ScheduledHours), 2),
            Math.Round(rows.Sum(x => x.TimeOffHours), 2),
            Math.Round(rows.Sum(x => x.PaidHours), 2),
            Math.Round(rows.Sum(x => x.GrossPay), 2));
    }

    private async Task EnsureEmployeeAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Employees.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.Id == employeeId && x.DeletedAtUtc == null, cancellationToken))
        {
            throw new InvalidOperationException("Employee was not found.");
        }
    }

    private async Task EnsureCompanyAdministratorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var isCompanyAdministrator = await dbContext.UserRoles.IgnoreQueryFilters()
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Role != null &&
                x.Role.OrganizationId == organizationId &&
                (x.Role.NormalizedName == "OWNER" || x.Role.NormalizedName == "ADMINISTRATOR"),
                cancellationToken);
        if (isCompanyAdministrator)
        {
            return;
        }

        var isPlatformAdministrator = await dbContext.PlatformUsers
            .AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "Platform Administrator", cancellationToken);
        if (isPlatformAdministrator)
        {
            return;
        }

        throw new UnauthorizedAccessException("Only company owners, company administrators, or platform administrators can manage business administration.");
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
            ConnectorPlaceholdersJson = DefaultConnectorConfigurationJson(),
            SupplierPreferencesJson = "{}"
        };
        dbContext.BusinessConfigurations.Add(config);
        return config;
    }

    private static IReadOnlyCollection<ConnectorDto> ReadConnectors(BusinessConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectorPlaceholdersJson))
        {
            return DefaultConnectors;
        }

        try
        {
            using var document = JsonDocument.Parse(config.ConnectorPlaceholdersJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return DefaultConnectors;
            }

            if (document.RootElement.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String))
            {
                return MergeWithDefaults(document.RootElement.EnumerateArray()
                    .Select(x => DefaultConnectorFromLegacyName(x.GetString()))
                    .Where(x => x is not null)
                    .Cast<ConnectorDto>()
                    .ToList());
            }

            var parsed = JsonSerializer.Deserialize<List<ConnectorDto>>(config.ConnectorPlaceholdersJson, ConnectorJsonOptions);
            return MergeWithDefaults(parsed ?? []);
        }
        catch (JsonException)
        {
            return DefaultConnectors;
        }
    }

    private async Task<IReadOnlyCollection<ConnectorDto>> BuildCompanySupplierConnectorsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var enabledSupplierCodes = await dbContext.TenantModuleEntitlements
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsEnabled && x.ModuleKey.StartsWith("SupplierConnector:"))
            .Select(x => x.ModuleKey)
            .ToListAsync(cancellationToken);
        var supplierCodes = enabledSupplierCodes
            .Select(TenantModuleCatalog.SupplierCodeFromModuleKey)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (supplierCodes.Count == 0)
        {
            return [];
        }

        var definitions = CompanySupplierConnectorDefinitions
            .Where(x => supplierCodes.Contains(x.SupplierCode))
            .ToArray();
        var supplierRows = await dbContext.Suppliers
            .AsNoTracking()
            .Where(x => supplierCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);
        var suppliersByCode = supplierRows.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var supplierIds = supplierRows.Select(x => x.Id).ToArray();
        var configurations = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && supplierIds.Contains(x.SupplierId))
            .ToListAsync(cancellationToken);
        var configurationsByConnector = configurations.ToDictionary(x => x.ConnectorKey, StringComparer.OrdinalIgnoreCase);
        var configurationIds = configurations.Select(x => x.Id).ToArray();
        var latestRuns = await dbContext.CompanySupplierConnectorImportRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && configurationIds.Contains(x.CompanySupplierConnectorConfigurationId) && x.Status == "Completed")
            .GroupBy(x => x.CompanySupplierConnectorConfigurationId)
            .Select(x => new
            {
                ConfigurationId = x.Key,
                LastCompletedAtUtc = x.Max(run => run.CompletedAtUtc)
            })
            .ToDictionaryAsync(x => x.ConfigurationId, x => x.LastCompletedAtUtc, cancellationToken);

        return definitions
            .Select(definition =>
            {
                configurationsByConnector.TryGetValue(definition.SupplierCode, out var configuration);
                suppliersByCode.TryGetValue(definition.SupplierCode, out var supplier);
                var credentialStatus = ConnectorCredentialStatus(definition, configuration);
                var status = configuration is null
                    ? "Enabled by platform"
                    : configuration.IsEnabled && credentialStatus == "Ready"
                        ? "Ready"
                        : configuration.IsEnabled
                            ? "Needs credentials"
                            : "Paused";
                var syncCadence = configuration is not null && configuration.SyncDealerPricingOnSchedule
                    ? $"{MinutesToDays(configuration.DealerPricingScheduleIntervalMinutes)} day"
                    : "Manual";
                var lastSyncAt = configuration is not null && latestRuns.TryGetValue(configuration.Id, out var lastRun)
                    ? lastRun
                    : null;

                return new ConnectorDto(
                    definition.Key,
                    supplier?.Name ?? definition.Name,
                    "Supplier",
                    status,
                    definition.Description,
                    configuration?.IsEnabled ?? false,
                    syncCadence,
                    lastSyncAt,
                    definition.Capabilities,
                    definition.SupplierCode == "WPS"
                        ? new WpsConnectorConfigurationDto(
                            configuration?.DealerAccountNumber ?? string.Empty,
                            configuration?.Username ?? string.Empty,
                            configuration?.BaseApiUrl ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            true,
                            true,
                            false,
                            false,
                            credentialStatus)
                        : null);
            })
            .OrderBy(x => x.Name)
            .ToArray();
    }

    private static IReadOnlyCollection<ConnectorDto> MergeWithDefaults(IReadOnlyCollection<ConnectorDto> storedConnectors)
    {
        var connectors = DefaultConnectors.ToDictionary(x => x.Key, x => x);
        foreach (var storedConnector in storedConnectors)
        {
            if (string.IsNullOrWhiteSpace(storedConnector.Key))
            {
                continue;
            }

            connectors[storedConnector.Key] = storedConnector;
        }

        return DefaultConnectors.Select(x => connectors[x.Key]).ToList();
    }

    private static ConnectorDto? DefaultConnectorFromLegacyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return DefaultConnectors.SingleOrDefault(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static ConnectorDto DefaultWpsConnector() => new(
        "wps",
        "WPS",
        "Supplier",
        "Not configured",
        "WPS catalog, inventory availability, pricing, and purchase order workflows.",
        false,
        "Manual",
        null,
        ["Catalog import", "Inventory availability", "Dealer pricing", "Purchase orders"],
        new WpsConnectorConfigurationDto(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, true, true, false, true, "Missing"));

    private static readonly CompanySupplierConnectorAdminDefinition[] CompanySupplierConnectorDefinitions =
    [
        new(
            "wps",
            "WPS",
            "Western Power Sports",
            "WPS catalog lookup, live warehouse inventory, fitment, and company dealer actual cost.",
            ["Catalog lookup", "Live inventory", "Fitment", "Company dealer actual cost"],
            false),
        new(
            "parts-unlimited",
            "PU",
            "Parts Unlimited",
            "Parts Unlimited catalog lookup, live warehouse inventory, fitment, and company dealer actual cost.",
            ["Catalog lookup", "Live inventory", "Fitment", "Company dealer actual cost"],
            false),
        new(
            "turn14",
            "TURN14",
            "Turn14",
            "Turn14 catalog lookup, live warehouse inventory, media, fitment, and company dealer actual cost.",
            ["Catalog lookup", "Live inventory", "Media", "Fitment", "Company dealer actual cost"],
            true)
    ];

    private static string ConnectorCredentialStatus(CompanySupplierConnectorAdminDefinition definition, CompanySupplierConnectorConfiguration? configuration)
    {
        if (configuration is null)
        {
            return "Missing";
        }

        return definition.RequiresSecret
            ? !string.IsNullOrWhiteSpace(configuration.ApiKey) && !string.IsNullOrWhiteSpace(configuration.ApiSecretProtected) ? "Ready" : "Missing"
            : !string.IsNullOrWhiteSpace(configuration.ApiKey) ? "Ready" : "Missing";
    }

    private static int MinutesToDays(int value)
    {
        return Math.Max(1, (int)Math.Ceiling((value <= 0 ? 1440 : value) / 1440m));
    }

    private static string NormalizeConnectorStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not configured";
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "READY" => "Ready",
            "PAUSED" => "Paused",
            _ => "Not configured"
        };
    }

    private static string NormalizeSyncCadence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Manual";
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "HOURLY" => "Hourly",
            "DAILY" => "Daily",
            "MANUAL" => "Manual",
            _ => "Manual"
        };
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
        config.LaborLineItemsTaxable,
        config.DefaultTaxRate,
        config.RegionalTaxOverridesJson,
        config.NumberSequencesJson,
        config.NotificationPreferencesJson,
        config.DepartmentsJson,
        ReadSupplierPreferences(config.SupplierPreferencesJson));

    private static SupplierPreferencesDto ReadSupplierPreferences(string? json)
    {
        StoredSupplierPreferences? stored = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                stored = JsonSerializer.Deserialize<StoredSupplierPreferences>(json, ConnectorJsonOptions);
            }
            catch (JsonException)
            {
            }
        }

        var preferredSupplierCode = NormalizeSupplierCode(stored?.PreferredSupplierCode);
        var preferredWarehouses = stored?.PreferredWarehouseCodes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var warehouses = DefaultSupplierWarehousePreferences
            .Select(supplier =>
            {
                preferredWarehouses.TryGetValue(supplier.SupplierCode, out var preferredWarehouseCode);
                return supplier with { PreferredWarehouseCode = Clean(preferredWarehouseCode) };
            })
            .ToList();
        return new SupplierPreferencesDto(preferredSupplierCode, warehouses);
    }

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

    private static TimeClockPunchDto ToPunchDto(EmployeeTimePunch punch, string employeeName, DateTimeOffset nowUtc) => new(
        punch.Id,
        punch.EmployeeId,
        employeeName,
        punch.ClockInUtc,
        punch.ClockOutUtc,
        punch.Hours ?? (punch.ClockOutUtc is DateTimeOffset clockOutUtc ? CalculateHours(punch.ClockInUtc, clockOutUtc) : CalculateHours(punch.ClockInUtc, nowUtc)),
        punch.Note,
        punch.ClockOutUtc is null);

    private static TimeClockScheduleShiftDto ToShiftDto(EmployeeScheduleShift shift, string employeeName) => new(
        shift.Id,
        shift.EmployeeId,
        employeeName,
        shift.ShiftDate,
        shift.StartTime,
        shift.EndTime,
        ShiftHours(shift),
        shift.Note);

    private static TimeClockTimeOffDto ToTimeOffDto(EmployeeTimeOffRequest timeOff, string employeeName) => new(
        timeOff.Id,
        timeOff.EmployeeId,
        employeeName,
        timeOff.Type,
        timeOff.StartDate,
        timeOff.EndDate,
        timeOff.HoursPerDay,
        InclusiveDays(timeOff.StartDate, timeOff.EndDate) * timeOff.HoursPerDay,
        timeOff.Status,
        timeOff.Note);

    private static EmployeeCompanyAssetDto ToAssetDto(EmployeeCompanyAsset asset, string employeeName) => new(
        asset.Id,
        asset.EmployeeId,
        employeeName,
        asset.Name,
        asset.AssetTag,
        asset.SerialNumber,
        asset.IssuedDate,
        asset.ReturnedDate,
        asset.Status,
        asset.Note);

    private static EmployeeSafetyIncidentDto ToSafetyIncidentDto(EmployeeSafetyIncident incident, string employeeName) => new(
        incident.Id,
        incident.EmployeeId,
        employeeName,
        incident.IncidentDate,
        incident.IncidentType,
        incident.Severity,
        incident.LostTimeHours,
        incident.IsOshaRecordable,
        incident.ReportedToOsha,
        incident.Description);

    private static object PunchSnapshot(EmployeeTimePunch punch) => new
    {
        punch.EmployeeId,
        punch.ClockInUtc,
        punch.ClockOutUtc,
        punch.Hours,
        punch.Note,
        punch.IsManualEntry,
        punch.Source
    };

    private static object ShiftSnapshot(EmployeeScheduleShift shift) => new
    {
        shift.EmployeeId,
        shift.ShiftDate,
        shift.StartTime,
        shift.EndTime,
        Hours = ShiftHours(shift),
        shift.Note
    };

    private static object TimeOffSnapshot(EmployeeTimeOffRequest timeOff) => new
    {
        timeOff.EmployeeId,
        timeOff.Type,
        timeOff.StartDate,
        timeOff.EndDate,
        timeOff.HoursPerDay,
        TotalHours = InclusiveDays(timeOff.StartDate, timeOff.EndDate) * timeOff.HoursPerDay,
        timeOff.Status,
        timeOff.Note
    };

    private static object AssetSnapshot(EmployeeCompanyAsset asset) => new
    {
        asset.EmployeeId,
        asset.Name,
        asset.AssetTag,
        asset.SerialNumber,
        asset.IssuedDate,
        asset.ReturnedDate,
        asset.Status,
        asset.Note
    };

    private static object SafetyIncidentSnapshot(EmployeeSafetyIncident incident) => new
    {
        incident.EmployeeId,
        incident.IncidentDate,
        incident.IncidentType,
        incident.Severity,
        incident.LostTimeHours,
        incident.IsOshaRecordable,
        incident.ReportedToOsha,
        incident.Description
    };

    private static decimal CalculateHours(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        Math.Max(0m, Math.Round((decimal)(endUtc - startUtc).TotalHours, 2));

    private static decimal ShiftHours(EmployeeScheduleShift shift) =>
        Math.Max(0m, Math.Round((decimal)(shift.EndTime - shift.StartTime).TotalHours, 2));

    private static int InclusiveDays(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    private static int OverlapDays(DateOnly start, DateOnly end, DateOnly periodStart, DateOnly periodEnd)
    {
        var overlapStart = start > periodStart ? start : periodStart;
        var overlapEnd = end < periodEnd ? end : periodEnd;
        return overlapEnd < overlapStart ? 0 : InclusiveDays(overlapStart, overlapEnd);
    }

    private static EmployeeCompensation? EffectiveHourlyCompensation(Employee employee, DateOnly periodEnd)
    {
        return employee.CompensationRecords
            .Where(x =>
                x.DeletedAtUtc == null &&
                x.PayrollType == "Hourly" &&
                x.HourlyRate is not null &&
                x.EffectiveStartDate <= periodEnd &&
                (x.EffectiveEndDate == null || x.EffectiveEndDate >= periodEnd))
            .OrderByDescending(x => x.EffectiveStartDate)
            .FirstOrDefault();
    }

    private static bool IsPaidTimeOffType(string type)
    {
        return type.Trim().ToUpperInvariant() is "PTO" or "VACATION" or "SICK" or "HOLIDAY";
    }

    private static (DateOnly Start, DateOnly End) CurrentPayrollPeriod(DateOnly localDate)
    {
        if (localDate.Day <= 15)
        {
            return (new DateOnly(localDate.Year, localDate.Month, 1), new DateOnly(localDate.Year, localDate.Month, 15));
        }

        return (new DateOnly(localDate.Year, localDate.Month, 16), new DateOnly(localDate.Year, localDate.Month, DateTime.DaysInMonth(localDate.Year, localDate.Month)));
    }

    private static DateOnly ToLocalDate(DateTimeOffset utc, string timeZoneId)
    {
        var local = TimeZoneInfo.ConvertTime(utc, FindTimeZone(timeZoneId));
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static DateTimeOffset ToUtc(DateTime localDateTime, string timeZoneId)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        var offset = FindTimeZone(timeZoneId).GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    private static DateTimeOffset StartOfLocalDateUtc(DateOnly localDate, string timeZoneId) => ToUtc(localDate.ToDateTime(TimeOnly.MinValue), timeZoneId);

    private static TimeZoneInfo FindTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static void ValidatePunchTimes(DateTimeOffset clockInUtc, DateTimeOffset? clockOutUtc)
    {
        if (clockOutUtc is not null && clockOutUtc <= clockInUtc)
        {
            throw new InvalidOperationException("Clock out must be after clock in.");
        }
    }

    private static string NormalizeClockAction(string? value)
    {
        var normalized = Required(value ?? string.Empty, "Clock action").Trim().ToUpperInvariant();
        return normalized switch
        {
            "IN" => "In",
            "OUT" => "Out",
            _ => throw new InvalidOperationException("Clock action must be In or Out.")
        };
    }

    private static string NormalizeTimeOffStatus(string? value)
    {
        var normalized = Required(value ?? string.Empty, "Time off status").Trim().ToUpperInvariant();
        return normalized switch
        {
            "APPROVE" or "APPROVED" => "Approved",
            "DECLINE" or "DECLINED" => "Declined",
            "PENDING" => "Pending",
            _ => throw new InvalidOperationException("Time off status must be Pending, Approved, or Declined.")
        };
    }

    private static string HashPin(Guid organizationId, string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{organizationId:N}:{pin}"));
        return Convert.ToHexString(bytes);
    }

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

    private static string? NormalizeSupplierCode(string? value)
    {
        var clean = Clean(value)?.ToUpperInvariant();
        return clean is "WPS" or "TURN14" or "PU" ? clean : null;
    }

    private static string Required(string value, string fieldName) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{fieldName} is required.") : value.Trim();

    private static string Defaulted(string? value, string? currentValue, string fallback) =>
        !string.IsNullOrWhiteSpace(value) ? value.Trim() : !string.IsNullOrWhiteSpace(currentValue) ? currentValue.Trim() : fallback;

    private static string FirstName(string name) => name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name.Trim();

    private static string? LastName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;
    }

    private static string ToTitleCase(string normalized) => string.Join(' ', normalized.Split('_').Select(word => word[..1] + word[1..].ToLowerInvariant()));

    private sealed record CompanySupplierConnectorAdminDefinition(
        string Key,
        string SupplierCode,
        string Name,
        string Description,
        IReadOnlyCollection<string> Capabilities,
        bool RequiresSecret);

    private sealed record StoredSupplierPreferences(
        string? PreferredSupplierCode,
        IReadOnlyDictionary<string, string> PreferredWarehouseCodes);
}
