using iM1os.Application.Platform;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class PlatformProvisioningTests
{
    [Fact]
    public async Task ProvisionAsync_creates_ready_tenant_owner_subscription_modules_email_events_and_audit()
    {
        await using var dbContext = CreateContext();
        var service = CreateProvisioningService(dbContext);

        var result = await service.ProvisionAsync(DefaultRequest(), "platform-user-1", CancellationToken.None);

        Assert.True(await dbContext.Organizations.IgnoreQueryFilters().AnyAsync(x => x.Id == result.OrganizationId));
        Assert.True(await dbContext.Locations.IgnoreQueryFilters().AnyAsync(x => x.Id == result.LocationId && x.OrganizationId == result.OrganizationId));
        Assert.True(await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == result.OwnerUserId && x.OrganizationId == result.OrganizationId));
        Assert.True(await dbContext.Roles.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == result.OrganizationId && x.NormalizedName == "OWNER"));
        Assert.True(await dbContext.PlatformSubscriptions.AnyAsync(x => x.OrganizationId == result.OrganizationId && x.Plan == "Professional"));
        Assert.Equal(2, await dbContext.TenantModuleEntitlements.CountAsync(x => x.OrganizationId == result.OrganizationId && x.IsEnabled));
        Assert.True(await dbContext.WelcomeEmails.AnyAsync(x => x.OrganizationId == result.OrganizationId && x.SentAtUtc != null));
        Assert.True(await dbContext.PlatformEvents.AnyAsync(x => x.TargetOrganizationId == result.OrganizationId && x.EventType == "TenantProvisioned"));
        Assert.True(await dbContext.PlatformAuditEvents.AnyAsync(x => x.TargetOrganizationId == result.OrganizationId && x.Action == "TenantProvisioned"));
    }

    [Fact]
    public async Task ProvisionAsync_rolls_back_when_welcome_email_fails()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = CreateOptions(databaseName);

        await using (var dbContext = CreateContext(options))
        {
            var service = CreateProvisioningService(dbContext, new FailingWelcomeEmailSender());

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(DefaultRequest(), "platform-user-1", CancellationToken.None));
        }

        await using var verificationContext = CreateContext(options);
        Assert.False(await verificationContext.Organizations.IgnoreQueryFilters().AnyAsync(x => x.Slug == "abc-motorsports"));
        Assert.False(await verificationContext.PlatformTenants.AnyAsync());
        Assert.False(await verificationContext.WelcomeEmails.AnyAsync());
    }

    [Fact]
    public async Task ProvisionAsync_rejects_duplicate_organization_slug()
    {
        await using var dbContext = CreateContext();
        dbContext.Organizations.Add(new Organization { Name = "ABC Motorsports", Slug = "abc-motorsports" });
        await dbContext.SaveChangesAsync();
        var service = CreateProvisioningService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(DefaultRequest(), "platform-user-1", CancellationToken.None));

        Assert.Contains("organization", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProvisionAsync_rejects_duplicate_owner_email()
    {
        await using var dbContext = CreateContext();
        dbContext.Users.Add(new ApplicationUser
        {
            OrganizationId = Guid.NewGuid(),
            Email = "owner@example.com",
            NormalizedEmail = "OWNER@EXAMPLE.COM",
            DisplayName = "Existing Owner",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateProvisioningService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(DefaultRequest(), "platform-user-1", CancellationToken.None));

        Assert.Contains("owner", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlatformAuthenticationService_authenticates_platform_users_only()
    {
        await using var dbContext = CreateContext();
        var hasher = new PasswordHasher<PlatformUser>();
        var platformUser = new PlatformUser
        {
            Email = "admin@im1os.com",
            NormalizedEmail = "ADMIN@IM1OS.COM",
            DisplayName = "Platform Admin",
            PasswordHash = string.Empty,
            Role = "Platform Administrator"
        };
        platformUser.PasswordHash = hasher.HashPassword(platformUser, "Password!123");
        dbContext.PlatformUsers.Add(platformUser);
        dbContext.Users.Add(new ApplicationUser
        {
            OrganizationId = Guid.NewGuid(),
            Email = "tenant@shop.test",
            NormalizedEmail = "TENANT@SHOP.TEST",
            DisplayName = "Tenant User",
            PasswordHash = "not-a-platform-password"
        });
        await dbContext.SaveChangesAsync();

        var service = new PlatformAuthenticationService(dbContext, hasher, new SystemClock());

        var platformLogin = await service.LoginAsync(new PlatformLoginRequest("admin@im1os.com", "Password!123"), CancellationToken.None);
        var tenantLogin = await service.LoginAsync(new PlatformLoginRequest("tenant@shop.test", "Password!123"), CancellationToken.None);

        Assert.NotNull(platformLogin);
        Assert.Null(tenantLogin);
        Assert.Equal("Platform Administrator", platformLogin.Role);
    }

    private static ProvisionTenantRequest DefaultRequest()
    {
        return new ProvisionTenantRequest(
            BusinessName: "ABC Motorsports",
            BusinessEmail: "info@abcmoto.test",
            OwnerName: "Alex Owner",
            OwnerEmail: "owner@example.com",
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

    private static TenantProvisioningService CreateProvisioningService(ApplicationDbContext dbContext, IWelcomeEmailSender? welcomeEmailSender = null)
    {
        return new TenantProvisioningService(
            dbContext,
            new PasswordHasher<ApplicationUser>(),
            welcomeEmailSender ?? new NoOpWelcomeEmailSender(),
            new SystemClock());
    }

    private static ApplicationDbContext CreateContext()
    {
        return CreateContext(CreateOptions(Guid.NewGuid().ToString()));
    }

    private static ApplicationDbContext CreateContext(DbContextOptions<ApplicationDbContext> options)
    {
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new SystemClock(), new TenantProvider(currentUser));
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private sealed class FailingWelcomeEmailSender : IWelcomeEmailSender
    {
        public Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Email failed.");
        }
    }
}
