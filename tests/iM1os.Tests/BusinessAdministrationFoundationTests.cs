using System.Security.Cryptography;
using System.Text;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.Employees;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class BusinessAdministrationFoundationTests
{
    [Fact]
    public async Task Owner_can_configure_profile_locations_employees_labor_and_audit_is_recorded()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var service = CreateService(dbContext);

        await service.UpdateBusinessProfileAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new UpdateBusinessProfileRequest(
                "ABC Motorsports",
                "ABC Motorsports LLC",
                "ABC Moto",
                "/images/abc.png",
                "https://abcmoto.test",
                "555-0100",
                "info@abcmoto.test",
                "12-3456789",
                "100 Main St",
                null,
                "Austin",
                "TX",
                "78701",
                "US",
                "America/Chicago",
                "en-US",
                "USD",
                "MM/dd/yyyy",
                "h:mm tt"),
            CancellationToken.None);
        await service.UpsertLocationAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new UpsertLocationRequest(null, "North Shop", "north", "555-0110", "200 North St", null, "Austin", "TX", "78702", "America/Chicago", 135m, "TX-AUSTIN", "Active"),
            CancellationToken.None);
        await service.InviteEmployeeAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new InviteEmployeeRequest("Sam Tech", "sam@abcmoto.test", "555-0111", "Technician", null),
            CancellationToken.None);
        await service.SaveLaborConfigurationAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new LaborConfigurationRequest(135m, 150m, 175m, 160m, 12m, 6m, false),
            CancellationToken.None);

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);

        Assert.Equal("ABC Motorsports LLC", workspace.Profile.LegalName);
        Assert.Contains(workspace.Locations, x => x.Code == "NORTH" && x.DefaultLaborRate == 135m);
        Assert.Contains(workspace.Employees, x => x.Email == "sam@abcmoto.test" && x.Role == "Technician" && x.Status == "Active");
        Assert.True(await dbContext.Employees.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == seeded.OrganizationId && x.Email == "sam@abcmoto.test"));
        Assert.Equal(135m, workspace.Configuration.DefaultLaborRate);
        Assert.False(workspace.Configuration.LaborLineItemsTaxable);
        Assert.True(await dbContext.AuditLogs.AnyAsync(x => x.OrganizationId == seeded.OrganizationId && x.Action == "EmployeeInvited"));
        Assert.True(await dbContext.TimelineEvents.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == seeded.OrganizationId && x.EventType == "LaborConfigurationUpdated"));
    }

    [Fact]
    public async Task Non_owner_cannot_access_business_administration()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var user = new ApplicationUser
        {
            OrganizationId = seeded.OrganizationId,
            Email = "writer@abcmoto.test",
            NormalizedEmail = "WRITER@ABCMOTO.TEST",
            DisplayName = "Service Writer",
            PasswordHash = "hash"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetWorkspaceAsync(seeded.OrganizationId, user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Administration_shows_enabled_supplier_status_read_only()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var service = CreateService(dbContext);
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = seeded.OrganizationId,
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            DealerAccountNumber = "D12345",
            Username = "wps-api-user",
            ApiKey = "api-key",
            AuthMode = "API Key",
            IsEnabled = true,
            SyncDealerPricingOnSchedule = true,
            DealerPricingScheduleIntervalMinutes = 2880
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        dbContext.TenantModuleEntitlements.Add(new TenantModuleEntitlement
        {
            OrganizationId = seeded.OrganizationId,
            ModuleKey = TenantModuleCatalog.WpsSupplierConnector,
            IsEnabled = true,
            EnabledAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);
        var wps = Assert.Single(workspace.Connectors);

        Assert.Equal("Ready", wps.Status);
        Assert.True(wps.IsEnabled);
        Assert.Equal("2 day", wps.SyncCadence);
        Assert.NotNull(wps.WpsConfiguration);
        Assert.Equal("D12345", wps.WpsConfiguration.DealerNumber);
        Assert.Equal("wps-api-user", wps.WpsConfiguration.Username);
        Assert.Equal("Ready", wps.WpsConfiguration.CredentialStatus);
    }

    [Fact]
    public async Task Platform_administrator_can_access_any_tenant_business_administration()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var platformUser = new PlatformUser
        {
            Email = "admin@im1os.com",
            NormalizedEmail = "ADMIN@IM1OS.COM",
            DisplayName = "Platform Administrator",
            PasswordHash = "hash",
            Role = "Platform Administrator"
        };
        dbContext.PlatformUsers.Add(platformUser);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, platformUser.Id, CancellationToken.None);

        Assert.Equal(seeded.OrganizationId, workspace.OrganizationId);
        Assert.Equal("ABC Motorsports", workspace.Profile.BusinessName);
    }

    [Fact]
    public async Task Time_clock_pin_punches_create_open_and_closed_punches()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var clock = new TestClock(new DateTimeOffset(2026, 7, 6, 14, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        await service.InviteEmployeeAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new InviteEmployeeRequest("Sam Tech", "sam-time@abcmoto.test", null, "Technician", null),
            CancellationToken.None);
        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == seeded.OrganizationId && x.Email == "sam-time@abcmoto.test");
        user.PinHash = HashPin(seeded.OrganizationId, "1234");
        await dbContext.SaveChangesAsync();

        await service.ClockEmployeeAsync(seeded.OrganizationId, seeded.OwnerId, new ClockEmployeeRequest(user.EmployeeId!.Value, "1234", "in"), CancellationToken.None);
        var openWorkspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);

        Assert.Single(openWorkspace.TimeClock.OpenPunches);

        clock.UtcNow = clock.UtcNow.AddMinutes(90);
        await service.ClockEmployeeAsync(seeded.OrganizationId, seeded.OwnerId, new ClockEmployeeRequest(user.EmployeeId.Value, "1234", "out"), CancellationToken.None);
        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);
        var punch = Assert.Single(workspace.TimeClock.RecentPunches);

        Assert.Empty(workspace.TimeClock.OpenPunches);
        Assert.Equal(1.5m, punch.Hours);
        Assert.Equal(1.5m, workspace.TimeClock.Payroll.TotalWorkedHours);
    }

    [Fact]
    public async Task Time_clock_schedule_and_approved_time_off_are_in_payroll_summary()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var clock = new TestClock(new DateTimeOffset(2026, 7, 6, 14, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        await service.InviteEmployeeAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new InviteEmployeeRequest("Riley Parts", "riley@abcmoto.test", null, "Inventory", null),
            CancellationToken.None);
        var employee = await dbContext.Employees.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == seeded.OrganizationId && x.Email == "riley@abcmoto.test");

        await service.AddScheduleShiftAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new AddScheduleShiftRequest(employee.Id, new DateOnly(2026, 7, 7), new TimeOnly(9, 0), new TimeOnly(17, 0), "Parts counter"),
            CancellationToken.None);
        await service.AddTimeOffAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new AddTimeOffRequest(employee.Id, "PTO", new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 8), 8m, null),
            CancellationToken.None);
        var timeOff = await dbContext.EmployeeTimeOffRequests.IgnoreQueryFilters().SingleAsync(x => x.EmployeeId == employee.Id);
        await service.SetTimeOffStatusAsync(seeded.OrganizationId, seeded.OwnerId, new SetTimeOffStatusRequest(timeOff.Id, "Approved"), CancellationToken.None);

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);
        var payrollRow = workspace.TimeClock.Payroll.Employees.Single(x => x.EmployeeId == employee.Id);

        Assert.Equal(8m, payrollRow.ScheduledHours);
        Assert.Equal(8m, payrollRow.TimeOffHours);
        Assert.Equal(0m, payrollRow.VarianceHours);
        Assert.Contains(workspace.TimeClock.TimeOffRequests, x => x.Id == timeOff.Id && x.Status == "Approved");
    }

    [Fact]
    public async Task Hourly_payroll_report_uses_worked_hours_paid_time_off_and_hourly_rate()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var clock = new TestClock(new DateTimeOffset(2026, 7, 6, 14, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        await service.InviteEmployeeAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new InviteEmployeeRequest("Hourly Tech", "hourly@abcmoto.test", null, "Technician", null),
            CancellationToken.None);
        var employee = await dbContext.Employees.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == seeded.OrganizationId && x.Email == "hourly@abcmoto.test");
        dbContext.EmployeeCompensations.Add(new EmployeeCompensation
        {
            OrganizationId = seeded.OrganizationId,
            EmployeeId = employee.Id,
            PayrollType = "Hourly",
            HourlyRate = 20m,
            EffectiveStartDate = new DateOnly(2026, 7, 1)
        });
        await dbContext.SaveChangesAsync();

        await service.AddTimePunchAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new AddTimePunchRequest(employee.Id, new DateTime(2026, 7, 7, 9, 0, 0), new DateTime(2026, 7, 7, 17, 0, 0), "Regular shift"),
            CancellationToken.None);
        await service.AddTimeOffAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new AddTimeOffRequest(employee.Id, "PTO", new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 8), 8m, null),
            CancellationToken.None);
        var timeOff = await dbContext.EmployeeTimeOffRequests.IgnoreQueryFilters().SingleAsync(x => x.EmployeeId == employee.Id);
        await service.SetTimeOffStatusAsync(seeded.OrganizationId, seeded.OwnerId, new SetTimeOffStatusRequest(timeOff.Id, "Approved"), CancellationToken.None);

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);
        var row = workspace.TimeClock.Payroll.Employees.Single(x => x.EmployeeId == employee.Id);

        Assert.Equal("Hourly", row.PayrollType);
        Assert.Equal(20m, row.HourlyRate);
        Assert.Equal(8m, row.WorkedHours);
        Assert.Equal(8m, row.PaidTimeOffHours);
        Assert.Equal(16m, row.PaidHours);
        Assert.Equal(320m, row.GrossPay);
        Assert.Equal(320m, workspace.TimeClock.Payroll.TotalGrossPay);
    }

    [Fact]
    public async Task Hr_assets_and_safety_incidents_are_available_in_administration_workspace()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedOwnerAsync(dbContext);
        var service = CreateService(dbContext, new TestClock(new DateTimeOffset(2026, 7, 6, 14, 0, 0, TimeSpan.Zero)));
        await service.InviteEmployeeAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new InviteEmployeeRequest("Safety Lead", "safety@abcmoto.test", null, "Manager", null),
            CancellationToken.None);
        var employee = await dbContext.Employees.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == seeded.OrganizationId && x.Email == "safety@abcmoto.test");

        await service.AddCompanyAssetAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new AddCompanyAssetRequest(employee.Id, "Shop Laptop", "LT-100", "SN123", new DateOnly(2026, 7, 1), null, "Issued for diagnostics"),
            CancellationToken.None);
        await service.AddSafetyIncidentAsync(
            seeded.OrganizationId,
            seeded.OwnerId,
            new AddSafetyIncidentRequest(employee.Id, new DateOnly(2026, 7, 5), "Slip", "First aid", 2.5m, true, false, "Wet floor"),
            CancellationToken.None);

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);
        var asset = Assert.Single(workspace.TimeClock.CompanyAssets);
        var incident = Assert.Single(workspace.TimeClock.SafetyIncidents);

        Assert.Equal("Shop Laptop", asset.Name);
        Assert.Equal("Issued", asset.Status);
        Assert.Equal("LT-100", asset.AssetTag);
        Assert.Equal("Slip", incident.IncidentType);
        Assert.True(incident.IsOshaRecordable);
        Assert.Equal(1, workspace.TimeClock.SafetySummary.IncidentCount);
        Assert.Equal(1, workspace.TimeClock.SafetySummary.OshaRecordableCount);
        Assert.Equal(2.5m, workspace.TimeClock.SafetySummary.LostTimeHours);
    }

    private static BusinessAdministrationService CreateService(ApplicationDbContext dbContext)
    {
        return new BusinessAdministrationService(dbContext, new SystemClock(), new PasswordHasher<ApplicationUser>());
    }

    private static BusinessAdministrationService CreateService(ApplicationDbContext dbContext, IDateTimeProvider clock)
    {
        return new BusinessAdministrationService(dbContext, clock, new PasswordHasher<ApplicationUser>());
    }

    private static async Task<(Guid OrganizationId, Guid OwnerId)> SeedOwnerAsync(ApplicationDbContext dbContext)
    {
        var organization = new Organization { Name = "ABC Motorsports", Slug = "abc-motorsports", OnboardingCompletedAtUtc = DateTimeOffset.UtcNow };
        var owner = new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = "owner@abcmoto.test",
            NormalizedEmail = "OWNER@ABCMOTO.TEST",
            DisplayName = "Alex Owner",
            PasswordHash = "hash"
        };
        var ownerRole = new Role { OrganizationId = organization.Id, Name = "Owner", NormalizedName = "OWNER", IsSystemRole = true };
        owner.UserRoles.Add(new UserRole { UserId = owner.Id, RoleId = ownerRole.Id });
        owner.OrganizationMemberships.Add(new OrganizationMembership { OrganizationId = organization.Id, UserId = owner.Id, DisplayName = owner.DisplayName });
        dbContext.Organizations.Add(organization);
        dbContext.Users.Add(owner);
        dbContext.Roles.Add(ownerRole);
        dbContext.Locations.Add(new Location { OrganizationId = organization.Id, Name = "Main", Code = "MAIN", DefaultLaborRate = 125m });
        dbContext.BusinessConfigurations.Add(new BusinessConfiguration { OrganizationId = organization.Id, DefaultLaborRate = 125m });
        await dbContext.SaveChangesAsync();
        return (organization.Id, owner.Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser,
            new SystemClock(),
            new TenantProvider(currentUser));
    }

    private static string HashPin(Guid organizationId, string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{organizationId:N}:{pin}"));
        return Convert.ToHexString(bytes);
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
