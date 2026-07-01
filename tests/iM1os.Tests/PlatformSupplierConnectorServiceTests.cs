using System.Net;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class PlatformSupplierConnectorServiceTests
{
    [Fact]
    public async Task Wps_connector_settings_are_saved_and_returned_without_echoing_secret()
    {
        var now = new DateTimeOffset(2026, 6, 29, 15, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SaveWpsConnectorAsync(new WpsConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.wps.test",
            MasterFileUrl: "https://data-depot.test/master-item-list.json",
            DealerAccountNumber: "D2240487",
            DataDepotPassword: "dealer-password",
            ApiKey: "api-key",
            ImportMasterFileOnSchedule: true,
            MasterFileImportMode: "ManualAndScheduled",
            MasterFileScheduleIntervalHours: 12,
            MasterFileScheduleMaxItems: 2000,
            ImportFitmentOnSchedule: true,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: 1000,
            FitmentScheduleFitmentLimit: 1,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.test"),
            "platform-user-1",
            CancellationToken.None);

        var page = await service.GetWpsConnectorAsync(CancellationToken.None);

        Assert.True(page.Settings.IsEnabled);
        Assert.Equal("https://api.wps.test", page.Settings.BaseApiUrl);
        Assert.Equal("https://data-depot.test/master-item-list.json", page.Settings.MasterFileUrl);
        Assert.Equal("D2240487", page.Settings.DealerAccountNumber);
        Assert.Equal(string.Empty, page.Settings.DataDepotPassword);
        Assert.Equal("api-key", page.Settings.ApiKey);
        Assert.Equal(12, page.Settings.MasterFileScheduleIntervalHours);
        Assert.Equal(720, await dbContext.SupplierConnectorConfigurations.Select(x => x.MasterFileScheduleCadenceMinutes).SingleAsync());
        Assert.Equal(2000, page.Settings.MasterFileScheduleMaxItems);
        Assert.True(page.Settings.ImportFitmentOnSchedule);
        Assert.Equal(24, page.Settings.FitmentScheduleIntervalHours);
        Assert.Equal(1440, await dbContext.SupplierConnectorConfigurations.Select(x => x.FitmentScheduleCadenceMinutes).SingleAsync());
        Assert.Equal(1000, page.Settings.FitmentScheduleMaxSkus);
        Assert.Equal(1, page.Settings.FitmentScheduleFitmentLimit);
        Assert.Equal(250, page.Settings.FitmentScheduleDelayMilliseconds);
        Assert.Equal("https://saas.indie-moto.test", page.Settings.FitmentSourceBaseUrl);
        Assert.True(page.MasterFileRemoteStatus.IsAvailable);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 18, 0, 0, TimeSpan.Zero), page.MasterFileRemoteStatus.LastModifiedUtc);
        Assert.True(page.Status.IsConfigured);
        Assert.Equal("dealer-password", await dbContext.SupplierConnectorConfigurations.Select(x => x.ApiSecretProtected).SingleAsync());
        Assert.True(await dbContext.PlatformEvents.AnyAsync(x => x.EventType == "SupplierConnectorSaved"));
    }

    [Fact]
    public async Task Wps_master_file_import_is_blocked_until_master_file_url_exists()
    {
        var now = new DateTimeOffset(2026, 6, 29, 15, 15, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.GetWpsConnectorAsync(CancellationToken.None);
        var configuration = await dbContext.SupplierConnectorConfigurations.SingleAsync();
        configuration.MasterFileUrl = null;
        await dbContext.SaveChangesAsync();

        var page = await service.ImportWpsMasterFileAsync(DefaultImportRequest(), "platform-user-1", CancellationToken.None);

        var importRun = Assert.Single(page.RecentImportRuns);
        Assert.Equal("Blocked", importRun.Status);
        Assert.Equal("Import blocked. Missing required settings: Master Item List URL.", importRun.Message);
    }

    [Fact]
    public async Task Wps_master_file_import_is_queued_when_connection_is_configured()
    {
        var now = new DateTimeOffset(2026, 6, 29, 15, 30, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SaveWpsConnectorAsync(new WpsConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.wps.test",
            MasterFileUrl: "https://data-depot.test/master-item-list.json",
            DealerAccountNumber: "D2240487",
            DataDepotPassword: "dealer-password",
            ApiKey: "api-key",
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.com"),
            "platform-user-1",
            CancellationToken.None);

        var page = await service.ImportWpsMasterFileAsync(DefaultImportRequest(), "platform-user-1", CancellationToken.None);

        var importRun = page.RecentImportRuns.Single(x => x.ImportType == "WpsMasterFile");
        Assert.Equal("Queued", importRun.Status);
        Assert.Equal("https://data-depot.test/master-item-list.json", importRun.Source);
        Assert.True(await dbContext.PlatformAuditEvents.AnyAsync(x => x.Action == "SupplierConnectorMasterFileImportRequested"));
    }

    [Fact]
    public async Task Wps_fitment_import_is_queued_separately()
    {
        var now = new DateTimeOffset(2026, 6, 29, 15, 45, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.GetWpsConnectorAsync(CancellationToken.None);

        var page = await service.ImportWpsFitmentAsync(
            new WpsFitmentImportRequest(
                ImportMode: "LimitedTest",
                MaxSkus: 500,
                FitmentLimit: 1,
                DelayMilliseconds: 250),
            "platform-user-1",
            CancellationToken.None);

        var importRun = page.RecentImportRuns.Single(x => x.ImportType == "WpsFitment");
        Assert.Equal("Queued", importRun.Status);
        Assert.Equal("https://saas.indie-moto.com", importRun.Source);
        Assert.True(await dbContext.PlatformAuditEvents.AnyAsync(x => x.Action == "SupplierConnectorFitmentImportRequested"));
    }

    [Fact]
    public async Task Manual_full_import_modes_ignore_posted_max_limits()
    {
        var now = new DateTimeOffset(2026, 6, 29, 15, 50, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.GetWpsConnectorAsync(CancellationToken.None);
        await service.ImportWpsMasterFileAsync(
            DefaultImportRequest() with { ImportMode = "Full", MaxItems = 500 },
            "platform-user-1",
            CancellationToken.None);
        await service.ImportWpsFitmentAsync(
            new WpsFitmentImportRequest("Full", 500, 1, 250),
            "platform-user-1",
            CancellationToken.None);

        await service.SaveTurn14ConnectorAsync(new Turn14ConnectorSettingsRequest(
            IsEnabled: true,
            BaseUrl: "https://turn14.test",
            Username: "turn14-user",
            Password: "turn14-password",
            ApiClientId: "turn14-client",
            ApiClientSecret: "turn14-secret",
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.com",
            ImportMediaOnSchedule: false,
            MediaScheduleIntervalHours: 24,
            MediaScheduleMaxItems: null,
            MediaScheduleDelayMilliseconds: 750),
            "platform-user-1",
            CancellationToken.None);
        await service.ImportTurn14MasterFileAsync(
            new Turn14MasterFileImportRequest(true, true, true, true, "Full", 500),
            "platform-user-1",
            CancellationToken.None);
        await service.ImportTurn14FitmentAsync(
            new Turn14FitmentImportRequest("Full", 500, 1, 250),
            "platform-user-1",
            CancellationToken.None);
        await service.ImportTurn14MediaEnrichmentAsync(
            new Turn14MediaEnrichmentImportRequest("Full", 500, 750),
            "platform-user-1",
            CancellationToken.None);

        await service.SavePartsUnlimitedConnectorAsync(new PartsUnlimitedConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.parts-unlimited.test/api",
            ApiKey: "pu-key",
            DealerPortalUsername: "dealer-user",
            DealerPortalPassword: "dealer-password",
            DealerPortalDealerCode: "P12345",
            BrandFileUrls: "https://files.parts-unlimited.test/brand.zip",
            BrandFileMaxFiles: null,
            ImportBrandImagesOnSchedule: false,
            BrandImagesScheduleIntervalHours: 24,
            BrandImagesScheduleMaxFiles: null,
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.com"),
            "platform-user-1",
            CancellationToken.None);
        await service.ImportPartsUnlimitedMasterFileAsync(
            new PartsUnlimitedMasterFileImportRequest(true, true, true, true, "Full", 500),
            "platform-user-1",
            CancellationToken.None);
        await service.ImportPartsUnlimitedFitmentAsync(
            new PartsUnlimitedFitmentImportRequest("Full", 500, 1, 250),
            "platform-user-1",
            CancellationToken.None);
        await service.ImportPartsUnlimitedBrandImagesAsync(
            new PartsUnlimitedBrandImagesImportRequest("Full", 500),
            "platform-user-1",
            CancellationToken.None);

        await AssertImportRunLimitIsNullAsync(dbContext, "WpsMasterFile", "MaxItems");
        await AssertImportRunLimitIsNullAsync(dbContext, "WpsFitment", "MaxSkus");
        await AssertImportRunLimitIsNullAsync(dbContext, "Turn14ProductLoadsheet", "MaxItems");
        await AssertImportRunLimitIsNullAsync(dbContext, "Turn14Fitment", "MaxSkus");
        await AssertImportRunLimitIsNullAsync(dbContext, "Turn14MediaEnrichment", "MaxItems");
        await AssertImportRunLimitIsNullAsync(dbContext, "PartsUnlimitedBundle", "MaxItems");
        await AssertImportRunLimitIsNullAsync(dbContext, "PartsUnlimitedFitment", "MaxSkus");
        await AssertImportRunLimitIsNullAsync(dbContext, "PartsUnlimitedBrandImages", "BrandFileMaxFiles");
    }

    [Fact]
    public async Task Global_scheduler_lists_connector_schedules_with_configurator_links()
    {
        var now = new DateTimeOffset(2026, 6, 29, 16, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SaveWpsConnectorAsync(new WpsConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.wps.test",
            MasterFileUrl: "https://data-depot.test/master-item-list.json",
            DealerAccountNumber: "D2240487",
            DataDepotPassword: "dealer-password",
            ApiKey: "api-key",
            ImportMasterFileOnSchedule: true,
            MasterFileImportMode: "ManualAndScheduled",
            MasterFileScheduleIntervalHours: 12,
            MasterFileScheduleMaxItems: 2000,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: 500,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.test"),
            "platform-user-1",
            CancellationToken.None);

        var page = await service.GetGlobalSchedulerAsync(CancellationToken.None);

        Assert.Equal(2, page.Events.Count);
        var master = page.Events.Single(x => x.EventType == "WpsMasterFile");
        Assert.True(master.IsEnabled);
        Assert.Equal("Platform", master.ConfiguratorController);
        Assert.Equal("WpsConnector", master.ConfiguratorAction);
        Assert.Equal(now, master.NextDueAtUtc);
        var fitment = page.Events.Single(x => x.EventType == "WpsFitment");
        Assert.False(fitment.IsEnabled);
        Assert.Null(fitment.NextDueAtUtc);
    }

    [Fact]
    public async Task Turn14_connector_settings_are_saved_without_echoing_password()
    {
        var now = new DateTimeOffset(2026, 6, 29, 16, 30, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SaveTurn14ConnectorAsync(new Turn14ConnectorSettingsRequest(
            IsEnabled: true,
            BaseUrl: "https://turn14.test",
            Username: "turn14-user",
            Password: "turn14-password",
            ApiClientId: "turn14-client",
            ApiClientSecret: "turn14-secret",
            ImportMasterFileOnSchedule: true,
            MasterFileImportMode: "ManualAndScheduled",
            MasterFileScheduleIntervalHours: 6,
            MasterFileScheduleMaxItems: 500,
            ImportFitmentOnSchedule: true,
            FitmentScheduleIntervalHours: 12,
            FitmentScheduleMaxSkus: 200,
            FitmentScheduleFitmentLimit: 1,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.test",
            ImportMediaOnSchedule: true,
            MediaScheduleIntervalHours: 6,
            MediaScheduleMaxItems: 300,
            MediaScheduleDelayMilliseconds: 750),
            "platform-user-1",
            CancellationToken.None);

        var page = await service.GetTurn14ConnectorAsync(CancellationToken.None);

        Assert.True(page.Settings.IsEnabled);
        Assert.Equal("https://turn14.test", page.Settings.BaseUrl);
        Assert.Equal("turn14-user", page.Settings.Username);
        Assert.Equal(string.Empty, page.Settings.Password);
        Assert.Equal("turn14-client", page.Settings.ApiClientId);
        Assert.Equal(string.Empty, page.Settings.ApiClientSecret);
        Assert.Equal(6, page.Settings.MasterFileScheduleIntervalHours);
        Assert.Equal(360, await dbContext.SupplierConnectorConfigurations.Select(x => x.MasterFileScheduleCadenceMinutes).SingleAsync());
        Assert.Equal(500, page.Settings.MasterFileScheduleMaxItems);
        Assert.True(page.Settings.ImportFitmentOnSchedule);
        Assert.Equal(12, page.Settings.FitmentScheduleIntervalHours);
        Assert.Equal(200, page.Settings.FitmentScheduleMaxSkus);
        Assert.Equal(1, page.Settings.FitmentScheduleFitmentLimit);
        Assert.True(page.Settings.ImportMediaOnSchedule);
        Assert.Equal(6, page.Settings.MediaScheduleIntervalHours);
        Assert.Equal(300, page.Settings.MediaScheduleMaxItems);
        Assert.True(page.Status.HasStoredCredentials);
        Assert.True(page.Status.HasApiCredentials);
        Assert.True(page.Status.IsConfigured);
        Assert.Contains("turn14-secret", await dbContext.SupplierConnectorConfigurations.Select(x => x.ApiSecretProtected).SingleAsync());
        Assert.True(await dbContext.PlatformEvents.AnyAsync(x => x.EventType == "Turn14ConnectorSaved"));
    }

    [Fact]
    public async Task Turn14_product_loadsheet_import_is_queued_when_credentials_exist()
    {
        var now = new DateTimeOffset(2026, 6, 29, 16, 45, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SaveTurn14ConnectorAsync(new Turn14ConnectorSettingsRequest(
            IsEnabled: true,
            BaseUrl: "https://turn14.test",
            Username: "turn14-user",
            Password: "turn14-password",
            ApiClientId: "turn14-client",
            ApiClientSecret: "turn14-secret",
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.com",
            ImportMediaOnSchedule: false,
            MediaScheduleIntervalHours: 24,
            MediaScheduleMaxItems: null,
            MediaScheduleDelayMilliseconds: 750),
            "platform-user-1",
            CancellationToken.None);

        var page = await service.ImportTurn14MasterFileAsync(new Turn14MasterFileImportRequest(
            ImportProducts: true,
            ImportSupplierPricing: true,
            UpdateExistingProducts: true,
            CreateMissingProducts: true,
            ImportMode: "LimitedTest",
            MaxItems: 500),
            "platform-user-1",
            CancellationToken.None);

        var importRun = page.RecentImportRuns.Single(x => x.ImportType == "Turn14ProductLoadsheet");
        Assert.Equal("Queued", importRun.Status);
        Assert.Equal("https://turn14.test", importRun.Source);
        Assert.Contains("first 500 rows", importRun.Message);
        Assert.True(await dbContext.PlatformAuditEvents.AnyAsync(x => x.Action == "Turn14ProductLoadsheetImportRequested"));
    }

    [Fact]
    public async Task Global_scheduler_lists_turn14_product_loadsheet_with_configurator_link()
    {
        var now = new DateTimeOffset(2026, 6, 29, 17, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SaveTurn14ConnectorAsync(new Turn14ConnectorSettingsRequest(
            IsEnabled: true,
            BaseUrl: "https://turn14.test",
            Username: "turn14-user",
            Password: "turn14-password",
            ApiClientId: "turn14-client",
            ApiClientSecret: "turn14-secret",
            ImportMasterFileOnSchedule: true,
            MasterFileImportMode: "Scheduled",
            MasterFileScheduleIntervalHours: 8,
            MasterFileScheduleMaxItems: 1000,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.com",
            ImportMediaOnSchedule: true,
            MediaScheduleIntervalHours: 6,
            MediaScheduleMaxItems: 500,
            MediaScheduleDelayMilliseconds: 750),
            "platform-user-1",
            CancellationToken.None);

        var page = await service.GetGlobalSchedulerAsync(CancellationToken.None);

        Assert.Equal(3, page.Events.Count);
        var loadsheet = page.Events.Single(x => x.EventType == "Turn14ProductLoadsheet");
        Assert.Equal("Turn14ProductLoadsheet", loadsheet.EventType);
        Assert.True(loadsheet.IsEnabled);
        Assert.Equal(480, loadsheet.CadenceMinutes);
        Assert.Equal("Platform", loadsheet.ConfiguratorController);
        Assert.Equal("Turn14Connector", loadsheet.ConfiguratorAction);
        Assert.Equal(now, loadsheet.NextDueAtUtc);
        var fitment = page.Events.Single(x => x.EventType == "Turn14Fitment");
        Assert.False(fitment.IsEnabled);
        Assert.Null(fitment.NextDueAtUtc);
        var media = page.Events.Single(x => x.EventType == "Turn14MediaEnrichment");
        Assert.True(media.IsEnabled);
        Assert.Equal(360, media.CadenceMinutes);
        Assert.Equal(now, media.NextDueAtUtc);
    }

    [Fact]
    public async Task Parts_unlimited_bundle_import_uses_cached_brand_ids_when_manual_brand_file_urls_are_blank()
    {
        var now = new DateTimeOffset(2026, 6, 30, 1, 45, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SavePartsUnlimitedConnectorAsync(new PartsUnlimitedConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.parts-unlimited.test/api",
            ApiKey: "pu-key",
            DealerPortalUsername: "dealer-user",
            DealerPortalPassword: "dealer-password",
            DealerPortalDealerCode: "P12345",
            BrandFileUrls: string.Empty,
            BrandFileMaxFiles: null,
            ImportBrandImagesOnSchedule: true,
            BrandImagesScheduleIntervalHours: 24,
            BrandImagesScheduleMaxFiles: null,
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: null,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.com"),
            "platform-user-1",
            CancellationToken.None);

        var configuration = await dbContext.SupplierConnectorConfigurations.SingleAsync();
        configuration.ApiSecretProtected = """
            {
              "ImportBrandFilesWithBundle": true,
              "BrandFileUrls": null,
              "BrandFileMaxFiles": null,
              "DealerPortalUsername": "dealer-user",
              "DealerPortalPassword": "dealer-password",
              "DealerPortalDealerCode": "P12345",
              "BrandCacheRefreshedAtUtc": "2026-06-30T01:36:00+00:00",
              "CachedBrands": [
                { "BrandId": "motion-pro", "DisplayName": "Motion Pro", "Count": 10, "FilteredCount": 10 }
              ]
            }
            """;
        await dbContext.SaveChangesAsync();

        var page = await service.ImportPartsUnlimitedMasterFileAsync(new PartsUnlimitedMasterFileImportRequest(
            ImportProducts: true,
            ImportSupplierPricing: true,
            UpdateExistingProducts: true,
            CreateMissingProducts: true,
            ImportMode: "LimitedTest",
            MaxItems: 1000),
            "platform-user-1",
            CancellationToken.None);

        var importRun = page.RecentImportRuns.Single(x => x.ImportType == "PartsUnlimitedBundle");
        Assert.Equal("Queued", importRun.Status);
        Assert.DoesNotContain("brand file URL", importRun.Message);

        page = await service.ImportPartsUnlimitedBrandImagesAsync(new PartsUnlimitedBrandImagesImportRequest(
            ImportMode: "LimitedTest",
            MaxFiles: 1),
            "platform-user-1",
            CancellationToken.None);

        var brandRun = page.RecentImportRuns.Single(x => x.ImportType == "PartsUnlimitedBrandImages");
        Assert.Equal("Queued", brandRun.Status);
        Assert.DoesNotContain("brand file URL", brandRun.Message);
    }

    [Fact]
    public async Task Supplier_item_fitment_fetch_queues_single_sku_fitment_run()
    {
        var now = new DateTimeOffset(2026, 6, 30, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());
        await service.SavePartsUnlimitedConnectorAsync(new PartsUnlimitedConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.parts-unlimited.test/api",
            ApiKey: "api-key",
            DealerPortalUsername: string.Empty,
            DealerPortalPassword: string.Empty,
            DealerPortalDealerCode: string.Empty,
            BrandFileUrls: string.Empty,
            BrandFileMaxFiles: null,
            ImportBrandImagesOnSchedule: false,
            BrandImagesScheduleIntervalHours: 24,
            BrandImagesScheduleMaxFiles: null,
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: 1,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.test"),
            "platform-user-1",
            CancellationToken.None);

        var supplier = await dbContext.Suppliers.SingleAsync(x => x.Code == "PU");
        var globalProduct = new GlobalProduct
        {
            Brand = "Moose",
            Description = "Brake pads",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = globalProduct.Id,
            SupplierSku = "003106",
            SupplierStatus = "Active"
        };
        dbContext.GlobalProducts.Add(globalProduct);
        dbContext.SupplierProducts.Add(supplierProduct);
        await dbContext.SaveChangesAsync();

        var result = await service.QueueSupplierItemFitmentAsync(supplierProduct.Id, "platform-user-1", CancellationToken.None);

        Assert.True(result.Queued);
        Assert.Contains("003106", result.Message);
        var importRun = await dbContext.SupplierConnectorImportRuns.SingleAsync(x => x.Id == result.ImportRunId);
        Assert.Equal("PartsUnlimitedFitment", importRun.ImportType);
        Assert.Equal("Queued", importRun.Status);
        Assert.Equal(1, importRun.ProgressTotal);
        using var document = JsonDocument.Parse(importRun.ParametersJson!);
        var root = document.RootElement;
        Assert.Equal("PU", root.GetProperty("SupplierCode").GetString());
        Assert.Equal("003106", root.GetProperty("Sku").GetString());
        Assert.Equal(1, root.GetProperty("MaxSkus").GetInt32());
        Assert.Equal(1, root.GetProperty("FitmentLimit").GetInt32());
        Assert.False(root.GetProperty("ExcludeNla").GetBoolean());
    }

    [Fact]
    public async Task Supplier_item_fitment_fetch_does_not_queue_when_fitment_already_exists()
    {
        var now = new DateTimeOffset(2026, 6, 30, 18, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var service = new PlatformSupplierConnectorService(dbContext, new TestClock(now), new StaticHeadHttpClientFactory());

        await service.SavePartsUnlimitedConnectorAsync(new PartsUnlimitedConnectorSettingsRequest(
            IsEnabled: true,
            BaseApiUrl: "https://api.parts-unlimited.test/api",
            ApiKey: "api-key",
            DealerPortalUsername: string.Empty,
            DealerPortalPassword: string.Empty,
            DealerPortalDealerCode: string.Empty,
            BrandFileUrls: string.Empty,
            BrandFileMaxFiles: null,
            ImportBrandImagesOnSchedule: false,
            BrandImagesScheduleIntervalHours: 24,
            BrandImagesScheduleMaxFiles: null,
            ImportMasterFileOnSchedule: false,
            MasterFileImportMode: "Manual",
            MasterFileScheduleIntervalHours: 24,
            MasterFileScheduleMaxItems: null,
            ImportFitmentOnSchedule: false,
            FitmentScheduleIntervalHours: 24,
            FitmentScheduleMaxSkus: null,
            FitmentScheduleFitmentLimit: 1,
            FitmentScheduleDelayMilliseconds: 250,
            FitmentSourceBaseUrl: "https://saas.indie-moto.test"),
            "platform-user-1",
            CancellationToken.None);

        var supplier = await dbContext.Suppliers.SingleAsync(x => x.Code == "PU");
        var globalProduct = new GlobalProduct
        {
            Brand = "Moose",
            Description = "Brake pads",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = globalProduct.Id,
            SupplierSku = "002069",
            SupplierStatus = "Active"
        };
        var fitmentRecord = new SupplierFitmentRecord
        {
            SupplierId = supplier.Id,
            SupplierProductId = supplierProduct.Id,
            SupplierKey = "PU",
            SupplierSku = "002069",
            Year = 2025,
            Make = "Yamaha",
            Model = "YZ250F",
            ResolutionStatus = "Resolved",
            ImportedAtUtc = now
        };
        dbContext.GlobalProducts.Add(globalProduct);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierFitmentRecords.Add(fitmentRecord);
        await dbContext.SaveChangesAsync();

        var result = await service.QueueSupplierItemFitmentAsync(supplierProduct.Id, "platform-user-1", CancellationToken.None);

        Assert.False(result.Queued);
        Assert.Null(result.ImportRunId);
        Assert.Contains("already has 1 fitment record", result.Message);
        Assert.DoesNotContain(await dbContext.SupplierConnectorImportRuns.ToListAsync(), x => x.ImportType == "PartsUnlimitedFitment");
    }

    private static WpsMasterFileImportRequest DefaultImportRequest()
    {
        return new WpsMasterFileImportRequest(
            ImportProducts: true,
            ImportSupplierPricing: true,
            ImportFitment: true,
            UpdateExistingProducts: true,
            CreateMissingProducts: true,
            ImportMode: "LimitedTest",
            MaxItems: 500);
    }

    private static ApplicationDbContext CreateContext(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(now), new TenantProvider(currentUser));
    }

    private static async Task AssertImportRunLimitIsNullAsync(ApplicationDbContext dbContext, string importType, string limitProperty)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns.SingleAsync(x => x.ImportType == importType);

        Assert.Null(importRun.ProgressTotal);
        Assert.DoesNotContain("first 500", importRun.Message ?? string.Empty);
        using var document = JsonDocument.Parse(importRun.ParametersJson ?? "{}");
        Assert.True(document.RootElement.TryGetProperty(limitProperty, out var value));
        Assert.Equal(JsonValueKind.Null, value.ValueKind);
    }

    private sealed class TestClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class StaticHeadHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHeadHttpMessageHandler());
        }
    }

    private sealed class StaticHeadHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            };
            response.Content.Headers.LastModified = new DateTimeOffset(2026, 6, 29, 18, 0, 0, TimeSpan.Zero);
            response.Content.Headers.ContentLength = 123456789;
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"test-etag\"");
            return Task.FromResult(response);
        }
    }
}
