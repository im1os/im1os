using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Application.Platform;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Platform;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class CompanySupplierServiceTests
{
    [Fact]
    public async Task Company_wps_settings_use_hours_but_persist_minutes()
    {
        var now = new DateTimeOffset(2026, 6, 29, 18, 0, 0, TimeSpan.Zero);
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext(now);
        var platformUser = AddPlatformAdmin(dbContext);
        AddSupplierEntitlement(dbContext, organizationId, TenantModuleCatalog.WpsSupplierConnector);
        var service = CreateService(dbContext, now);

        var page = await service.SaveWpsConnectorAsync(
            organizationId,
            platformUser.Id,
            new CompanyWpsConnectorSettingsRequest(
                IsEnabled: true,
                BaseApiUrl: "https://api.wps.test",
                DealerAccountNumber: "D2240487",
                Username: "admin",
                ApiKey: "api-key",
                ApiSecret: "secret",
                SyncDealerPricingOnSchedule: true,
                DealerPricingScheduleIntervalHours: 12,
                DealerPricingScheduleMaxItems: 200),
            CancellationToken.None);

        Assert.Equal(12, page.Settings.DealerPricingScheduleIntervalHours);
        var configuration = await dbContext.CompanySupplierConnectorConfigurations.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(720, configuration.DealerPricingScheduleIntervalMinutes);
    }

    [Fact]
    public async Task Company_wps_page_reports_last_price_file_metadata()
    {
        var now = new DateTimeOffset(2026, 6, 29, 18, 0, 0, TimeSpan.Zero);
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext(now);
        var platformUser = AddPlatformAdmin(dbContext);
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            ApiKey = "api-key",
            AuthMode = "API Key",
            IsEnabled = true
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        dbContext.CompanySupplierConnectorImportRuns.Add(new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = "WpsDealerPricing",
            Status = "Completed",
            RequestedAtUtc = now.AddMinutes(-5),
            CompletedAtUtc = now,
            Source = "https://files.wps.test/dealer-pricing.csv",
            ParametersJson = """{"PriceFileUrl":"https://files.wps.test/dealer-pricing.csv","PriceFileLastModifiedUtc":"2026-06-29T16:30:00+00:00","PriceFileDownloadedAtUtc":"2026-06-29T17:55:00+00:00"}""",
            Message = "WPS dealer pricing sync completed."
        });
        await dbContext.SaveChangesAsync();
        AddSupplierEntitlement(dbContext, organizationId, TenantModuleCatalog.WpsSupplierConnector);
        var service = CreateService(dbContext, now);

        var page = await service.GetWpsConnectorAsync(organizationId, platformUser.Id, CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 6, 29, 16, 30, 0, TimeSpan.Zero), page.PriceFileStatus.PriceFileLastModifiedUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 17, 55, 0, TimeSpan.Zero), page.PriceFileStatus.LastDownloadedAtUtc);
        Assert.Equal(now, page.PriceFileStatus.LastAppliedAtUtc);
        Assert.Equal("https://files.wps.test/dealer-pricing.csv", page.PriceFileStatus.Source);
    }

    private static PlatformUser AddPlatformAdmin(ApplicationDbContext dbContext)
    {
        var user = new PlatformUser
        {
            Email = "admin@im1os.test",
            NormalizedEmail = "ADMIN@IM1OS.TEST",
            DisplayName = "Admin",
            PasswordHash = "hash",
            Role = "Platform Administrator",
            IsActive = true
        };
        dbContext.PlatformUsers.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    private static void AddSupplierEntitlement(ApplicationDbContext dbContext, Guid organizationId, string moduleKey)
    {
        dbContext.TenantModuleEntitlements.Add(new TenantModuleEntitlement
        {
            OrganizationId = organizationId,
            ModuleKey = moduleKey,
            IsEnabled = true,
            EnabledAtUtc = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static CompanySupplierService CreateService(ApplicationDbContext dbContext, DateTimeOffset now)
    {
        return new CompanySupplierService(dbContext, new TestClock(now), new TenantModuleEntitlementService(dbContext));
    }

    private static ApplicationDbContext CreateContext(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(now), new TenantProvider(currentUser));
    }

    private sealed class TestClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;
    }
}
