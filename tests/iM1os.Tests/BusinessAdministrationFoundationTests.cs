using iM1os.Application.BusinessAdministration;
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
            new LaborConfigurationRequest(135m, 150m, 175m, 160m, 12m, 6m),
            CancellationToken.None);

        var workspace = await service.GetWorkspaceAsync(seeded.OrganizationId, seeded.OwnerId, CancellationToken.None);

        Assert.Equal("ABC Motorsports LLC", workspace.Profile.LegalName);
        Assert.Contains(workspace.Locations, x => x.Code == "NORTH" && x.DefaultLaborRate == 135m);
        Assert.Contains(workspace.Employees, x => x.Email == "sam@abcmoto.test" && x.Role == "Technician" && x.Status == "Invited");
        Assert.Equal(135m, workspace.Configuration.DefaultLaborRate);
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

    private static BusinessAdministrationService CreateService(ApplicationDbContext dbContext)
    {
        return new BusinessAdministrationService(dbContext, new SystemClock(), new PasswordHasher<ApplicationUser>());
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
}
