using iM1os.Application.Platform;
using iM1os.Application.TenantIdentity;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class TenantIdentityExperienceTests
{
    [Fact]
    public async Task Provisioning_creates_owner_invitation_and_welcome_activation_link()
    {
        await using var dbContext = CreateContext();
        var emailSender = new CapturingWelcomeEmailSender();
        var provisioning = CreateProvisioningService(dbContext, emailSender);

        var result = await provisioning.ProvisionAsync(DefaultProvisionRequest(), "platform-user-1", CancellationToken.None);

        var owner = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == result.OwnerUserId);
        Assert.False(owner.IsActive);
        Assert.True(owner.MustChangePassword);
        Assert.True(await dbContext.UserInvitations.IgnoreQueryFilters().AnyAsync(x => x.UserId == owner.Id && x.AcceptedAtUtc == null));
        Assert.Contains("/company/activate?token=", emailSender.LastBody);
        Assert.True(await dbContext.PlatformEvents.AnyAsync(x => x.EventType == "OwnerInvited" && x.TargetOrganizationId == result.OrganizationId));
    }

    [Fact]
    public async Task ActivateOwner_sets_password_verifies_email_logs_in_and_requires_onboarding()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedInvitedOwnerAsync(dbContext);
        var service = CreateTenantIdentityService(dbContext);

        var result = await service.ActivateOwnerAsync(
            new ActivateOwnerRequest(seeded.Token, "NewPassword!123", "NewPassword!123"),
            "127.0.0.1",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.RequiresOnboarding);
        var owner = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == seeded.UserId);
        Assert.True(owner.IsActive);
        Assert.NotNull(owner.EmailVerifiedAtUtc);
        Assert.False(owner.MustChangePassword);
        Assert.True(await dbContext.TenantIdentityEvents.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == seeded.OrganizationId && x.EventType == "OwnerActivated"));
        Assert.True(await dbContext.TenantIdentityEvents.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == seeded.OrganizationId && x.EventType == "UserLoggedIn"));
    }

    [Fact]
    public async Task Login_resolves_organization_roles_permissions_and_logout_records_event()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedActiveOwnerAsync(dbContext);
        var service = CreateTenantIdentityService(dbContext);

        var result = await service.LoginAsync(new TenantLoginRequest("owner@shop.test", "Password!12345", null, true), "127.0.0.1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(seeded.OrganizationId, result.OrganizationId);
        Assert.Contains("Owner", result.Roles);

        await service.LogoutAsync(seeded.OrganizationId, seeded.UserId, "127.0.0.1", CancellationToken.None);
        Assert.True(await dbContext.TenantIdentityEvents.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == seeded.OrganizationId && x.EventType == "UserLoggedOut"));
    }

    [Fact]
    public async Task Login_locks_account_after_repeated_failures()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedActiveOwnerAsync(dbContext);
        var service = CreateTenantIdentityService(dbContext);

        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await service.LoginAsync(new TenantLoginRequest("owner@shop.test", "bad-password", null, false), null, CancellationToken.None));
        }

        var owner = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == seeded.UserId);
        Assert.NotNull(owner.LockoutEndAtUtc);
    }

    [Fact]
    public async Task Password_reset_completes_and_allows_new_password_login()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedActiveOwnerAsync(dbContext);
        var service = CreateTenantIdentityService(dbContext);
        var token = TenantIdentityService.NewToken();
        dbContext.PasswordResetRequests.Add(new PasswordResetRequest
        {
            OrganizationId = seeded.OrganizationId,
            UserId = seeded.UserId,
            TokenHash = TenantIdentityService.HashToken(token),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var completed = await service.CompletePasswordResetAsync(new CompletePasswordResetRequest(token, "ResetPassword!123", "ResetPassword!123"), null, CancellationToken.None);
        var login = await service.LoginAsync(new TenantLoginRequest("owner@shop.test", "ResetPassword!123", seeded.OrganizationId, false), null, CancellationToken.None);

        Assert.True(completed);
        Assert.NotNull(login);
        Assert.True(await dbContext.TenantIdentityEvents.IgnoreQueryFilters().AnyAsync(x => x.EventType == "PasswordResetCompleted"));
    }

    [Fact]
    public async Task Owner_onboarding_completion_updates_dashboard_readiness()
    {
        await using var dbContext = CreateContext();
        var seeded = await SeedActiveOwnerAsync(dbContext);
        var service = new BusinessOnboardingService(dbContext, new SystemClock());

        await service.CompleteAsync(seeded.OrganizationId, seeded.UserId, DefaultOnboardingRequest(), null, CancellationToken.None);
        var dashboard = await service.GetDashboardAsync(seeded.OrganizationId, CancellationToken.None);

        Assert.Equal("ABC Motorsports", dashboard.BusinessName);
        Assert.Equal(100, dashboard.SetupProgress);
        Assert.True(await dbContext.TenantIdentityEvents.IgnoreQueryFilters().AnyAsync(x => x.EventType == "BusinessOnboardingCompleted"));
    }

    private static ProvisionTenantRequest DefaultProvisionRequest()
    {
        return new ProvisionTenantRequest(
            BusinessName: "ABC Motorsports",
            BusinessEmail: "info@abcmoto.test",
            OwnerName: "Alex Owner",
            OwnerEmail: "owner@shop.test",
            Phone: "555-0100",
            AddressLine1: "100 Main St",
            AddressLine2: null,
            City: "Austin",
            Region: "TX",
            PostalCode: "78701",
            Country: "US",
            TimeZone: "America/Chicago",
            SubscriptionPlan: "Professional",
            IsTrial: true,
            DefaultModules: ["Service", "Parts"],
            DefaultLanguage: "en-US",
            DefaultCurrency: "USD");
    }

    private static BusinessOnboardingRequest DefaultOnboardingRequest()
    {
        return new BusinessOnboardingRequest(
            "ABC Motorsports",
            "info@abcmoto.test",
            "555-0100",
            "America/Chicago",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "8:00 AM - 5:00 PM",
            "Closed",
            "Closed",
            125m,
            true,
            true,
            true);
    }

    private static TenantProvisioningService CreateProvisioningService(ApplicationDbContext dbContext, IWelcomeEmailSender welcomeEmailSender)
    {
        return new TenantProvisioningService(
            dbContext,
            new PasswordHasher<ApplicationUser>(),
            welcomeEmailSender,
            new SystemClock());
    }

    private static TenantIdentityService CreateTenantIdentityService(ApplicationDbContext dbContext, IWelcomeEmailSender? welcomeEmailSender = null)
    {
        return new TenantIdentityService(
            dbContext,
            new PasswordHasher<ApplicationUser>(),
            welcomeEmailSender ?? new CapturingWelcomeEmailSender(),
            new SystemClock());
    }

    private static async Task<(Guid OrganizationId, Guid UserId, string Token)> SeedInvitedOwnerAsync(ApplicationDbContext dbContext)
    {
        var organization = new Organization { Name = "ABC Motorsports", Slug = "abc-motorsports" };
        var ownerRole = new Role { OrganizationId = organization.Id, Name = "Owner", NormalizedName = "OWNER", IsSystemRole = true };
        var owner = new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = "owner@shop.test",
            NormalizedEmail = "OWNER@SHOP.TEST",
            DisplayName = "Alex Owner",
            PasswordHash = "not-active",
            IsActive = false,
            MustChangePassword = true
        };
        owner.UserRoles.Add(new UserRole { UserId = owner.Id, RoleId = ownerRole.Id });
        var token = TenantIdentityService.NewToken();
        dbContext.Organizations.Add(organization);
        dbContext.Roles.Add(ownerRole);
        dbContext.Users.Add(owner);
        dbContext.PlatformTenants.Add(new PlatformTenant
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            Slug = organization.Slug,
            Status = "Trial",
            SubscriptionPlan = "Professional",
            CurrentVersion = "v0.1.0",
            HealthStatus = "Healthy",
            ActiveUsers = 1,
            Locations = 1,
            BillingStatus = "Trial",
            ProvisioningStatus = "Provisioned"
        });
        dbContext.UserInvitations.Add(new UserInvitation
        {
            OrganizationId = organization.Id,
            UserId = owner.Id,
            Email = owner.Email,
            TokenHash = TenantIdentityService.HashToken(token),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        });
        await dbContext.SaveChangesAsync();
        return (organization.Id, owner.Id, token);
    }

    private static async Task<(Guid OrganizationId, Guid UserId)> SeedActiveOwnerAsync(ApplicationDbContext dbContext)
    {
        var seeded = await SeedInvitedOwnerAsync(dbContext);
        var owner = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == seeded.UserId);
        owner.IsActive = true;
        owner.MustChangePassword = false;
        owner.EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
        owner.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(owner, "Password!12345");
        dbContext.Locations.Add(new Location
        {
            OrganizationId = seeded.OrganizationId,
            Name = "Main",
            Code = "MAIN",
            Phone = "555-0100",
            AddressLine1 = "100 Main St",
            City = "Austin",
            Region = "TX",
            PostalCode = "78701"
        });
        await dbContext.SaveChangesAsync();
        return (seeded.OrganizationId, seeded.UserId);
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

    private sealed class CapturingWelcomeEmailSender : IWelcomeEmailSender
    {
        public string LastBody { get; private set; } = string.Empty;

        public Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken)
        {
            LastBody = body;
            return Task.CompletedTask;
        }
    }
}
