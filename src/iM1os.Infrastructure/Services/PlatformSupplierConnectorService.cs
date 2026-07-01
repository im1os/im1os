using System.Net;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class PlatformSupplierConnectorService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IHttpClientFactory httpClientFactory) : IPlatformSupplierConnectorService
{
    private const string WpsConnectorKey = "WPS";
    private const string WpsSupplierCode = "WPS";
    private const string WpsSupplierName = "Western Power Sports";
    private const string Turn14ConnectorKey = "TURN14";
    private const string Turn14SupplierCode = "TURN14";
    private const string Turn14SupplierName = "Turn 14 Distribution";
    private const string PartsUnlimitedConnectorKey = "PU";
    private const string PartsUnlimitedSupplierCode = "PU";
    private const string PartsUnlimitedSupplierName = "Parts Unlimited";
    private const string WpsDefaultBaseApiUrl = "https://api.wps-inc.com";
    private const string WpsDefaultMasterFileUrl = "https://data-depot.s3.us-west-2.amazonaws.com/v4/downloads/master-item-list/master-item-list.json";
    private const string Turn14DefaultBaseUrl = "https://turn14.com";
    private const string PartsUnlimitedDefaultBaseApiUrl = "https://api.parts-unlimited.com/api";
    private const string PartsUnlimitedDefaultBundlePath = "/v1/parts/bundle";
    private const string DefaultFitmentSourceBaseUrl = "https://saas.indie-moto.com";

    public async Task<WpsConnectorPage> GetWpsConnectorAsync(CancellationToken cancellationToken)
    {
        var configuration = await EnsureWpsConfigurationAsync(cancellationToken);
        return await BuildPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<Turn14ConnectorPage> GetTurn14ConnectorAsync(CancellationToken cancellationToken)
    {
        var configuration = await EnsureTurn14ConfigurationAsync(cancellationToken);
        return await BuildTurn14PageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> GetPartsUnlimitedConnectorAsync(CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<GlobalSchedulerPage> GetGlobalSchedulerAsync(CancellationToken cancellationToken)
    {
        var schedules = await (
            from configuration in dbContext.SupplierConnectorConfigurations.AsNoTracking()
            join supplier in dbContext.Suppliers.AsNoTracking() on configuration.SupplierId equals supplier.Id
            select new
            {
                configuration.Id,
                configuration.ConnectorKey,
                configuration.DisplayName,
                configuration.IsEnabled,
                SupplierName = supplier.Name,
                configuration.ImportMasterFileOnSchedule,
                configuration.MasterFileScheduleCadenceMinutes,
                configuration.LastMasterFileScheduledAtUtc,
                configuration.ImportFitmentOnSchedule,
                configuration.FitmentScheduleCadenceMinutes,
                configuration.LastFitmentScheduledAtUtc,
                configuration.ImportMediaOnSchedule,
                configuration.MediaScheduleCadenceMinutes,
                configuration.MediaScheduleMaxItems,
                configuration.MediaScheduleDelayMilliseconds,
                configuration.LastMediaScheduledAtUtc
            })
            .ToListAsync(cancellationToken);

        var rows = new List<GlobalSchedulerEventRow>();
        foreach (var schedule in schedules)
        {
            if (string.Equals(schedule.ConnectorKey, Turn14ConnectorKey, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(await BuildSchedulerRowAsync(
                    schedule.Id,
                    $"{schedule.DisplayName} Product Loadsheet",
                    schedule.SupplierName,
                    "Turn14ProductLoadsheet",
                    schedule.IsEnabled && schedule.ImportMasterFileOnSchedule,
                    schedule.MasterFileScheduleCadenceMinutes <= 0 ? 1440 : schedule.MasterFileScheduleCadenceMinutes,
                    schedule.LastMasterFileScheduledAtUtc,
                    "Platform",
                    "Turn14Connector",
                    cancellationToken));
                rows.Add(await BuildSchedulerRowAsync(
                    schedule.Id,
                    $"{schedule.DisplayName} Fitment",
                    schedule.SupplierName,
                    "Turn14Fitment",
                    schedule.IsEnabled && schedule.ImportFitmentOnSchedule,
                    schedule.FitmentScheduleCadenceMinutes <= 0 ? 1440 : schedule.FitmentScheduleCadenceMinutes,
                    schedule.LastFitmentScheduledAtUtc,
                    "Platform",
                    "Turn14Connector",
                    cancellationToken));
                rows.Add(await BuildSchedulerRowAsync(
                    schedule.Id,
                    $"{schedule.DisplayName} Media Enrichment",
                    schedule.SupplierName,
                    "Turn14MediaEnrichment",
                    schedule.IsEnabled && schedule.ImportMediaOnSchedule,
                    schedule.MediaScheduleCadenceMinutes <= 0 ? 1440 : schedule.MediaScheduleCadenceMinutes,
                    schedule.LastMediaScheduledAtUtc,
                    "Platform",
                    "Turn14Connector",
                    cancellationToken));
                continue;
            }

            if (string.Equals(schedule.ConnectorKey, PartsUnlimitedConnectorKey, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(await BuildSchedulerRowAsync(
                    schedule.Id,
                    $"{schedule.DisplayName} Bundle",
                    schedule.SupplierName,
                    "PartsUnlimitedBundle",
                    schedule.IsEnabled && schedule.ImportMasterFileOnSchedule,
                    schedule.MasterFileScheduleCadenceMinutes <= 0 ? 1440 : schedule.MasterFileScheduleCadenceMinutes,
                    schedule.LastMasterFileScheduledAtUtc,
                    "Platform",
                    "PartsUnlimitedConnector",
                    cancellationToken));
                rows.Add(await BuildSchedulerRowAsync(
                    schedule.Id,
                    $"{schedule.DisplayName} Fitment",
                    schedule.SupplierName,
                    "PartsUnlimitedFitment",
                    schedule.IsEnabled && schedule.ImportFitmentOnSchedule,
                    schedule.FitmentScheduleCadenceMinutes <= 0 ? 1440 : schedule.FitmentScheduleCadenceMinutes,
                    schedule.LastFitmentScheduledAtUtc,
                    "Platform",
                    "PartsUnlimitedConnector",
                    cancellationToken));
                rows.Add(await BuildSchedulerRowAsync(
                    schedule.Id,
                    $"{schedule.DisplayName} Brand Images",
                    schedule.SupplierName,
                    "PartsUnlimitedBrandImages",
                    schedule.IsEnabled && schedule.ImportMediaOnSchedule,
                    schedule.MediaScheduleCadenceMinutes <= 0 ? 1440 : schedule.MediaScheduleCadenceMinutes,
                    schedule.LastMediaScheduledAtUtc,
                    "Platform",
                    "PartsUnlimitedConnector",
                    cancellationToken));
                continue;
            }

            rows.Add(await BuildSchedulerRowAsync(
                schedule.Id,
                $"{schedule.DisplayName} Master File",
                schedule.SupplierName,
                "WpsMasterFile",
                schedule.IsEnabled && schedule.ImportMasterFileOnSchedule,
                schedule.MasterFileScheduleCadenceMinutes <= 0 ? 1440 : schedule.MasterFileScheduleCadenceMinutes,
                schedule.LastMasterFileScheduledAtUtc,
                "Platform",
                "WpsConnector",
                cancellationToken));

            rows.Add(await BuildSchedulerRowAsync(
                schedule.Id,
                $"{schedule.DisplayName} Fitment",
                schedule.SupplierName,
                "WpsFitment",
                schedule.IsEnabled && schedule.ImportFitmentOnSchedule,
                schedule.FitmentScheduleCadenceMinutes <= 0 ? 1440 : schedule.FitmentScheduleCadenceMinutes,
                schedule.LastFitmentScheduledAtUtc,
                "Platform",
                "WpsConnector",
                cancellationToken));
        }

        return new GlobalSchedulerPage(rows
            .OrderByDescending(x => x.IsEnabled)
            .ThenBy(x => x.Owner)
            .ThenBy(x => x.Name)
            .ToList());
    }

    public async Task<WpsConnectorPage> SaveWpsConnectorAsync(WpsConnectorSettingsRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureWpsConfigurationAsync(cancellationToken);

        configuration.IsEnabled = request.IsEnabled;
        configuration.BaseApiUrl = Clean(request.BaseApiUrl) ?? WpsDefaultBaseApiUrl;
        configuration.MasterFileUrl = Clean(request.MasterFileUrl) ?? WpsDefaultMasterFileUrl;
        configuration.DealerAccountNumber = Clean(request.DealerAccountNumber);
        configuration.Username = null;
        configuration.ApiKey = Clean(request.ApiKey);
        if (!string.IsNullOrWhiteSpace(request.DataDepotPassword))
        {
            configuration.ApiSecretProtected = request.DataDepotPassword.Trim();
        }

        configuration.AuthMode = "DataDepotDealerLoginAndApiKey";
        configuration.ImportMasterFileOnSchedule = request.ImportMasterFileOnSchedule;
        configuration.MasterFileImportMode = Required(request.MasterFileImportMode, "Master File import mode");
        configuration.MasterFileScheduleCadenceMinutes = HoursToMinutes(request.MasterFileScheduleIntervalHours);
        configuration.MasterFileScheduleMaxItems = PositiveOrNull(request.MasterFileScheduleMaxItems);
        configuration.ImportFitmentOnSchedule = request.ImportFitmentOnSchedule;
        configuration.FitmentScheduleCadenceMinutes = HoursToMinutes(request.FitmentScheduleIntervalHours);
        configuration.FitmentScheduleMaxSkus = PositiveOrNull(request.FitmentScheduleMaxSkus);
        configuration.FitmentScheduleFitmentLimit = PositiveOrNull(request.FitmentScheduleFitmentLimit);
        configuration.FitmentScheduleDelayMilliseconds = ClampPositive(request.FitmentScheduleDelayMilliseconds, 0, 5000);
        configuration.FitmentSourceBaseUrl = Clean(request.FitmentSourceBaseUrl) ?? DefaultFitmentSourceBaseUrl;

        AddAuditEvent("SupplierConnectorSaved", platformUserId, new
        {
            configuration.ConnectorKey,
            configuration.IsEnabled,
            configuration.BaseApiUrl,
            configuration.MasterFileUrl,
            HasDealerId = !string.IsNullOrWhiteSpace(configuration.DealerAccountNumber),
            HasDataDepotPassword = !string.IsNullOrWhiteSpace(configuration.ApiSecretProtected),
            HasApiKey = !string.IsNullOrWhiteSpace(configuration.ApiKey),
            configuration.ImportMasterFileOnSchedule,
            configuration.MasterFileImportMode,
            configuration.MasterFileScheduleCadenceMinutes,
            configuration.MasterFileScheduleMaxItems,
            configuration.ImportFitmentOnSchedule,
            configuration.FitmentScheduleCadenceMinutes,
            configuration.FitmentScheduleMaxSkus,
            configuration.FitmentScheduleFitmentLimit,
            configuration.FitmentScheduleDelayMilliseconds,
            configuration.FitmentSourceBaseUrl
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<WpsConnectorPage> TestWpsConnectionAsync(string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureWpsConfigurationAsync(cancellationToken);
        var missingFields = RequiredConnectionFields(configuration).ToArray();
        var now = dateTimeProvider.UtcNow;

        configuration.LastConnectionTestAtUtc = now;
        if (missingFields.Length > 0)
        {
            configuration.LastConnectionStatus = "ConfigurationIncomplete";
            configuration.LastConnectionMessage = $"Missing required settings: {string.Join(", ", missingFields)}.";
        }
        else
        {
            configuration.LastConnectionStatus = "Ready";
            configuration.LastConnectionMessage = "Connection settings are complete. Live WPS API validation is pending connector client implementation.";
        }

        AddAuditEvent("SupplierConnectorConnectionTested", platformUserId, new
        {
            configuration.ConnectorKey,
            configuration.LastConnectionStatus,
            configuration.LastConnectionMessage
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<Turn14ConnectorPage> SaveTurn14ConnectorAsync(Turn14ConnectorSettingsRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureTurn14ConfigurationAsync(cancellationToken);

        configuration.IsEnabled = request.IsEnabled;
        configuration.BaseApiUrl = Clean(request.BaseUrl) ?? Turn14DefaultBaseUrl;
        configuration.Username = Clean(request.Username);
        configuration.ApiKey = Clean(request.ApiClientId);
        configuration.ApiSecretProtected = Turn14ConnectorSecrets.MergeSecretJson(
            configuration.ApiSecretProtected,
            request.Password,
            request.ApiClientSecret);

        configuration.AuthMode = "CookieLogin";
        configuration.MasterFileUrl = "POST /export.php stockExport=items";
        configuration.ImportMasterFileOnSchedule = request.ImportMasterFileOnSchedule;
        configuration.MasterFileImportMode = Required(request.MasterFileImportMode, "Product loadsheet import mode");
        configuration.MasterFileScheduleCadenceMinutes = HoursToMinutes(request.MasterFileScheduleIntervalHours);
        configuration.MasterFileScheduleMaxItems = PositiveOrNull(request.MasterFileScheduleMaxItems);
        configuration.ImportFitmentOnSchedule = request.ImportFitmentOnSchedule;
        configuration.FitmentScheduleCadenceMinutes = HoursToMinutes(request.FitmentScheduleIntervalHours);
        configuration.FitmentScheduleMaxSkus = PositiveOrNull(request.FitmentScheduleMaxSkus);
        configuration.FitmentScheduleFitmentLimit = PositiveOrNull(request.FitmentScheduleFitmentLimit);
        configuration.FitmentScheduleDelayMilliseconds = ClampPositive(request.FitmentScheduleDelayMilliseconds, 0, 5000);
        configuration.FitmentSourceBaseUrl = Clean(request.FitmentSourceBaseUrl) ?? DefaultFitmentSourceBaseUrl;
        configuration.ImportMediaOnSchedule = request.ImportMediaOnSchedule;
        configuration.MediaScheduleCadenceMinutes = HoursToMinutes(request.MediaScheduleIntervalHours);
        configuration.MediaScheduleMaxItems = PositiveOrNull(request.MediaScheduleMaxItems);
        configuration.MediaScheduleDelayMilliseconds = ClampPositive(request.MediaScheduleDelayMilliseconds, 0, 5000);
        var secrets = Turn14ConnectorSecrets.FromConfiguration(configuration);

        AddAuditEvent("Turn14ConnectorSaved", platformUserId, new
        {
            configuration.ConnectorKey,
            configuration.IsEnabled,
            configuration.BaseApiUrl,
            HasEnvironmentCredentials = HasTurn14EnvironmentCredentials(),
            HasStoredUsername = !string.IsNullOrWhiteSpace(configuration.Username),
            HasWebCredentials = secrets.HasWebCredentials,
            HasApiCredentials = secrets.HasApiCredentials,
            configuration.ImportMasterFileOnSchedule,
            configuration.MasterFileImportMode,
            configuration.MasterFileScheduleCadenceMinutes,
            configuration.MasterFileScheduleMaxItems,
            configuration.ImportFitmentOnSchedule,
            configuration.FitmentScheduleCadenceMinutes,
            configuration.FitmentScheduleMaxSkus,
            configuration.FitmentScheduleFitmentLimit,
            configuration.FitmentScheduleDelayMilliseconds,
            configuration.FitmentSourceBaseUrl,
            configuration.ImportMediaOnSchedule,
            configuration.MediaScheduleCadenceMinutes,
            configuration.MediaScheduleMaxItems,
            configuration.MediaScheduleDelayMilliseconds
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildTurn14PageAsync(configuration.Id, cancellationToken);
    }

    public async Task<Turn14ConnectorPage> TestTurn14ConnectionAsync(string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureTurn14ConfigurationAsync(cancellationToken);
        var missingFields = RequiredTurn14ConnectionFields(configuration).ToArray();
        var now = dateTimeProvider.UtcNow;

        configuration.LastConnectionTestAtUtc = now;
        if (missingFields.Length > 0)
        {
            configuration.LastConnectionStatus = "ConfigurationIncomplete";
            configuration.LastConnectionMessage = $"Missing required settings: {string.Join(", ", missingFields)}.";
        }
        else
        {
            configuration.LastConnectionStatus = "Ready";
            configuration.LastConnectionMessage = "Connection settings are complete. Turn14 login will be validated by the next product loadsheet import.";
        }

        AddAuditEvent("Turn14ConnectorConnectionTested", platformUserId, new
        {
            configuration.ConnectorKey,
            configuration.LastConnectionStatus,
            configuration.LastConnectionMessage
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildTurn14PageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> SavePartsUnlimitedConnectorAsync(PartsUnlimitedConnectorSettingsRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);

        configuration.IsEnabled = request.IsEnabled;
        configuration.BaseApiUrl = Clean(request.BaseApiUrl) ?? PartsUnlimitedDefaultBaseApiUrl;
        configuration.MasterFileUrl = PartsUnlimitedDefaultBundlePath;
        configuration.ApiKey = Clean(request.ApiKey);
        var existingPartsUnlimitedOptions = PartsUnlimitedConnectorOptions.FromConfiguration(configuration);
        configuration.ApiSecretProtected = JsonSerializer.Serialize(new PartsUnlimitedConnectorOptions(
            false,
            Clean(request.BrandFileUrls),
            PositiveOrNull(request.BrandFileMaxFiles),
            Clean(request.DealerPortalUsername),
            Clean(request.DealerPortalPassword) ?? existingPartsUnlimitedOptions.DealerPortalPassword,
            Clean(request.DealerPortalDealerCode),
            existingPartsUnlimitedOptions.BrandCacheRefreshedAtUtc,
            existingPartsUnlimitedOptions.CachedBrands));
        configuration.AuthMode = "ApiKeyHeader";
        configuration.ImportMasterFileOnSchedule = request.ImportMasterFileOnSchedule;
        configuration.MasterFileImportMode = Required(request.MasterFileImportMode, "Bundle import mode");
        configuration.MasterFileScheduleCadenceMinutes = HoursToMinutes(request.MasterFileScheduleIntervalHours);
        configuration.MasterFileScheduleMaxItems = PositiveOrNull(request.MasterFileScheduleMaxItems);
        configuration.ImportFitmentOnSchedule = request.ImportFitmentOnSchedule;
        configuration.FitmentScheduleCadenceMinutes = HoursToMinutes(request.FitmentScheduleIntervalHours);
        configuration.FitmentScheduleMaxSkus = PositiveOrNull(request.FitmentScheduleMaxSkus);
        configuration.FitmentScheduleFitmentLimit = PositiveOrNull(request.FitmentScheduleFitmentLimit);
        configuration.FitmentScheduleDelayMilliseconds = ClampPositive(request.FitmentScheduleDelayMilliseconds, 0, 5000);
        configuration.FitmentSourceBaseUrl = Clean(request.FitmentSourceBaseUrl) ?? DefaultFitmentSourceBaseUrl;
        configuration.ImportMediaOnSchedule = request.ImportBrandImagesOnSchedule;
        configuration.MediaScheduleCadenceMinutes = HoursToMinutes(request.BrandImagesScheduleIntervalHours);
        configuration.MediaScheduleMaxItems = PositiveOrNull(request.BrandImagesScheduleMaxFiles);
        configuration.MediaScheduleDelayMilliseconds = 750;

        AddAuditEvent("PartsUnlimitedConnectorSaved", platformUserId, new
        {
            configuration.ConnectorKey,
            configuration.IsEnabled,
            configuration.BaseApiUrl,
            HasApiKey = !string.IsNullOrWhiteSpace(configuration.ApiKey),
            HasDealerPortalUsername = !string.IsNullOrWhiteSpace(request.DealerPortalUsername),
            HasDealerPortalPassword = !string.IsNullOrWhiteSpace(Clean(request.DealerPortalPassword) ?? existingPartsUnlimitedOptions.DealerPortalPassword),
            HasDealerPortalDealerCode = !string.IsNullOrWhiteSpace(request.DealerPortalDealerCode),
            HasBrandFileUrls = !string.IsNullOrWhiteSpace(request.BrandFileUrls),
            BrandFileMaxFiles = PositiveOrNull(request.BrandFileMaxFiles),
            request.ImportBrandImagesOnSchedule,
            configuration.MediaScheduleCadenceMinutes,
            configuration.MediaScheduleMaxItems,
            configuration.ImportMasterFileOnSchedule,
            configuration.MasterFileImportMode,
            configuration.MasterFileScheduleCadenceMinutes,
            configuration.MasterFileScheduleMaxItems,
            configuration.ImportFitmentOnSchedule,
            configuration.FitmentScheduleCadenceMinutes,
            configuration.FitmentScheduleMaxSkus,
            configuration.FitmentScheduleFitmentLimit,
            configuration.FitmentScheduleDelayMilliseconds,
            configuration.FitmentSourceBaseUrl
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> TestPartsUnlimitedConnectionAsync(string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);
        var missingFields = RequiredPartsUnlimitedConnectionFields(configuration).ToArray();
        var now = dateTimeProvider.UtcNow;

        configuration.LastConnectionTestAtUtc = now;
        if (missingFields.Length > 0)
        {
            configuration.LastConnectionStatus = "ConfigurationIncomplete";
            configuration.LastConnectionMessage = $"Missing required settings: {string.Join(", ", missingFields)}.";
        }
        else
        {
            configuration.LastConnectionStatus = "Ready";
            configuration.LastConnectionMessage = "Connection settings are complete. The Parts Unlimited API key will be used by the next bundle import.";
        }

        AddAuditEvent("PartsUnlimitedConnectorConnectionTested", platformUserId, new
        {
            configuration.ConnectorKey,
            configuration.LastConnectionStatus,
            configuration.LastConnectionMessage
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> RefreshPartsUnlimitedBrandCacheAsync(string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);
        var options = PartsUnlimitedConnectorOptions.FromConfiguration(configuration);
        var missingFields = RequiredPartsUnlimitedDealerPortalFields(options).ToArray();
        if (missingFields.Length > 0)
        {
            configuration.LastConnectionStatus = "ConfigurationIncomplete";
            configuration.LastConnectionMessage = $"Brand cache refresh blocked. Missing required settings: {string.Join(", ", missingFields)}.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
        }

        IReadOnlyCollection<PartsUnlimitedCachedBrandRow> brands;
        try
        {
            brands = await FetchPartsUnlimitedBrandsAsync(options, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            var checkedAtUtc = dateTimeProvider.UtcNow;
            configuration.LastConnectionTestAtUtc = checkedAtUtc;
            configuration.LastConnectionStatus = "ConnectionFailed";
            configuration.LastConnectionMessage = exception.StatusCode == HttpStatusCode.Unauthorized
                ? "Parts Unlimited dealer portal login failed. Verify Dealer Portal User ID, password, and dealer number."
                : $"Parts Unlimited brand cache refresh failed: {exception.Message}";

            AddAuditEvent("PartsUnlimitedBrandCacheRefreshFailed", platformUserId, new
            {
                configuration.ConnectorKey,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage,
                exception.StatusCode,
                CheckedAtUtc = checkedAtUtc
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var checkedAtUtc = dateTimeProvider.UtcNow;
            configuration.LastConnectionTestAtUtc = checkedAtUtc;
            configuration.LastConnectionStatus = "ConnectionFailed";
            configuration.LastConnectionMessage = "Parts Unlimited brand cache refresh timed out while contacting the dealer portal.";

            AddAuditEvent("PartsUnlimitedBrandCacheRefreshTimedOut", platformUserId, new
            {
                configuration.ConnectorKey,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage,
                CheckedAtUtc = checkedAtUtc
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
        }

        var refreshedAtUtc = dateTimeProvider.UtcNow;
        configuration.ApiSecretProtected = JsonSerializer.Serialize(options with
        {
            BrandCacheRefreshedAtUtc = refreshedAtUtc,
            CachedBrands = brands
        });
        configuration.LastConnectionTestAtUtc = refreshedAtUtc;
        configuration.LastConnectionStatus = "Ready";
        configuration.LastConnectionMessage = $"Parts Unlimited brand cache refreshed. Cached {brands.Count:N0} active brands.";

        AddAuditEvent("PartsUnlimitedBrandCacheRefreshed", platformUserId, new
        {
            configuration.ConnectorKey,
            BrandCount = brands.Count,
            RefreshedAtUtc = refreshedAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<Turn14ConnectorPage> ImportTurn14MasterFileAsync(Turn14MasterFileImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureTurn14ConfigurationAsync(cancellationToken);
        var missingFields = RequiredTurn14ConnectionFields(configuration).ToArray();
        var maxItems = EffectiveManualLimit(request.ImportMode, request.MaxItems);
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? Turn14LimitedImportMessage(maxItems)
            : $"Import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "Turn14ProductLoadsheet",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = configuration.BaseApiUrl ?? Turn14DefaultBaseUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxItems,
            ParametersJson = JsonSerializer.Serialize(new
            {
                request.ImportProducts,
                request.ImportSupplierPricing,
                request.UpdateExistingProducts,
                request.CreateMissingProducts,
                request.ImportMode,
                MaxItems = maxItems
            }),
            Message = message
        });

        AddAuditEvent("Turn14ProductLoadsheetImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportProducts,
            request.ImportSupplierPricing,
            request.UpdateExistingProducts,
            request.CreateMissingProducts,
            request.ImportMode,
            MaxItems = maxItems
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildTurn14PageAsync(configuration.Id, cancellationToken);
    }

    public async Task<Turn14ConnectorPage> ImportTurn14FitmentAsync(Turn14FitmentImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureTurn14ConfigurationAsync(cancellationToken);
        var missingFields = RequiredFitmentFields(configuration).ToArray();
        var maxSkus = EffectiveManualLimit(request.ImportMode, request.MaxSkus);
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? Turn14LimitedFitmentMessage(maxSkus)
            : $"Fitment import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "Turn14Fitment",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxSkus,
            ParametersJson = JsonSerializer.Serialize(new
            {
                SupplierCode = Turn14SupplierCode,
                request.ImportMode,
                MaxSkus = maxSkus,
                request.FitmentLimit,
                DelayMilliseconds = ClampPositive(request.DelayMilliseconds, 0, 5000),
                BaseUrl = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
                ExcludeNla = true
            }),
            Message = message
        });

        AddAuditEvent("Turn14FitmentImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportMode,
            MaxSkus = maxSkus,
            request.FitmentLimit,
            request.DelayMilliseconds
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildTurn14PageAsync(configuration.Id, cancellationToken);
    }

    public async Task<Turn14ConnectorPage> ImportTurn14MediaEnrichmentAsync(Turn14MediaEnrichmentImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureTurn14ConfigurationAsync(cancellationToken);
        var secrets = Turn14ConnectorSecrets.FromConfiguration(configuration);
        var maxItems = EffectiveManualLimit(request.ImportMode, request.MaxItems);
        var now = dateTimeProvider.UtcNow;
        var status = secrets.HasApiCredentials ? "Queued" : "Blocked";
        var message = secrets.HasApiCredentials
            ? Turn14LimitedMediaMessage(maxItems)
            : "Media enrichment blocked. Missing required settings: TURN14_CLIENT_ID and TURN14_CLIENT_SECRET.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "Turn14MediaEnrichment",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = "https://api.turn14.com/v1/items/data/{itemId}",
            ProgressProcessed = 0,
            ProgressTotal = maxItems,
            ParametersJson = JsonSerializer.Serialize(new
            {
                request.ImportMode,
                MaxItems = maxItems,
                DelayMilliseconds = ClampPositive(request.DelayMilliseconds, 0, 5000)
            }),
            Message = message
        });

        AddAuditEvent("Turn14MediaEnrichmentImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportMode,
            MaxItems = maxItems,
            request.DelayMilliseconds
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildTurn14PageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> ImportPartsUnlimitedMasterFileAsync(PartsUnlimitedMasterFileImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);
        var missingFields = RequiredPartsUnlimitedConnectionFields(configuration).ToArray();
        var maxItems = EffectiveManualLimit(request.ImportMode, request.MaxItems);
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? PartsUnlimitedLimitedImportMessage(maxItems)
            : $"Import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = configuration.BaseApiUrl ?? PartsUnlimitedDefaultBaseApiUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxItems,
            ParametersJson = JsonSerializer.Serialize(new
            {
                request.ImportProducts,
                request.ImportSupplierPricing,
                request.UpdateExistingProducts,
                request.CreateMissingProducts,
                request.ImportMode,
                MaxItems = maxItems
            }),
            Message = message
        });

        AddAuditEvent("PartsUnlimitedBundleImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportProducts,
            request.ImportSupplierPricing,
            request.UpdateExistingProducts,
            request.CreateMissingProducts,
            request.ImportMode,
            MaxItems = maxItems
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> ImportPartsUnlimitedBrandImagesAsync(PartsUnlimitedBrandImagesImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);
        var options = PartsUnlimitedConnectorOptions.FromConfiguration(configuration);
        var missingFields = RequiredPartsUnlimitedBrandImageFields(configuration, options).ToArray();
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var maxFiles = EffectiveManualLimit(request.ImportMode, request.MaxFiles);
        var message = missingFields.Length == 0
            ? (maxFiles is null
                ? "Parts Unlimited brand image import queued."
                : $"Parts Unlimited brand image import queued for first {maxFiles.Value:N0} brand files.")
            : $"Brand image import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBrandImages",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = "Parts Unlimited brand reports",
            ProgressProcessed = 0,
            ProgressTotal = maxFiles,
            ParametersJson = JsonSerializer.Serialize(new
            {
                ImportBrandFiles = true,
                BrandFileUrls = options.BrandFileUrls,
                BrandFileMaxFiles = maxFiles ?? options.BrandFileMaxFiles,
                request.ImportMode
            }),
            Message = message
        });

        AddAuditEvent("PartsUnlimitedBrandImagesImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportMode,
            MaxFiles = maxFiles,
            HasBrandFileUrls = !string.IsNullOrWhiteSpace(options.BrandFileUrls),
            CachedBrandCount = options.CachedBrands.Count
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<PartsUnlimitedConnectorPage> ImportPartsUnlimitedFitmentAsync(PartsUnlimitedFitmentImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsurePartsUnlimitedConfigurationAsync(cancellationToken);
        var missingFields = RequiredFitmentFields(configuration).ToArray();
        var maxSkus = EffectiveManualLimit(request.ImportMode, request.MaxSkus);
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? PartsUnlimitedLimitedFitmentMessage(maxSkus)
            : $"Fitment import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedFitment",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxSkus,
            ParametersJson = JsonSerializer.Serialize(new
            {
                SupplierCode = PartsUnlimitedSupplierCode,
                request.ImportMode,
                MaxSkus = maxSkus,
                request.FitmentLimit,
                DelayMilliseconds = ClampPositive(request.DelayMilliseconds, 0, 5000),
                BaseUrl = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
                ExcludeNla = true
            }),
            Message = message
        });

        AddAuditEvent("PartsUnlimitedFitmentImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportMode,
            MaxSkus = maxSkus,
            request.FitmentLimit,
            request.DelayMilliseconds
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPartsUnlimitedPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<WpsConnectorPage> ImportWpsMasterFileAsync(WpsMasterFileImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureWpsConfigurationAsync(cancellationToken);
        var missingFields = RequiredMasterFileFields(configuration).ToArray();
        var maxItems = EffectiveManualLimit(request.ImportMode, request.MaxItems);
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? LimitedImportMessage(maxItems)
            : $"Import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "WpsMasterFile",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = configuration.MasterFileUrl ?? WpsDefaultMasterFileUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxItems,
            ParametersJson = JsonSerializer.Serialize(new
            {
                request.ImportProducts,
                request.ImportSupplierPricing,
                request.ImportFitment,
                request.UpdateExistingProducts,
                request.CreateMissingProducts,
                request.ImportMode,
                MaxItems = maxItems
            }),
            Message = message
        });

        AddAuditEvent("SupplierConnectorMasterFileImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportProducts,
            request.ImportSupplierPricing,
            request.ImportFitment,
            request.UpdateExistingProducts,
            request.CreateMissingProducts,
            request.ImportMode,
            MaxItems = maxItems
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<WpsConnectorPage> ImportWpsFitmentAsync(WpsFitmentImportRequest request, string? platformUserId, CancellationToken cancellationToken)
    {
        var configuration = await EnsureWpsConfigurationAsync(cancellationToken);
        var missingFields = RequiredFitmentFields(configuration).ToArray();
        var maxSkus = EffectiveManualLimit(request.ImportMode, request.MaxSkus);
        var now = dateTimeProvider.UtcNow;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? LimitedFitmentMessage(maxSkus)
            : $"Fitment import blocked. Missing required settings: {string.Join(", ", missingFields)}.";

        dbContext.SupplierConnectorImportRuns.Add(new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "WpsFitment",
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxSkus,
            ParametersJson = JsonSerializer.Serialize(new
            {
                SupplierCode = WpsSupplierCode,
                request.ImportMode,
                MaxSkus = maxSkus,
                request.FitmentLimit,
                DelayMilliseconds = ClampPositive(request.DelayMilliseconds, 0, 5000),
                BaseUrl = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
                ExcludeNla = true
            }),
            Message = message
        });

        AddAuditEvent("SupplierConnectorFitmentImportRequested", platformUserId, new
        {
            configuration.ConnectorKey,
            status,
            request.ImportMode,
            MaxSkus = maxSkus,
            request.FitmentLimit,
            request.DelayMilliseconds
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(configuration.Id, cancellationToken);
    }

    public async Task<SupplierItemFitmentQueueResult> QueueSupplierItemFitmentAsync(Guid supplierProductId, string? platformUserId, CancellationToken cancellationToken)
    {
        var supplierProduct = await dbContext.SupplierProducts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == supplierProductId, cancellationToken);
        if (supplierProduct is null)
        {
            return new SupplierItemFitmentQueueResult(false, "Supplier item was not found.", null);
        }

        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == supplierProduct.SupplierId, cancellationToken);
        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == supplier.Code, cancellationToken);
        if (configuration is null)
        {
            return new SupplierItemFitmentQueueResult(false, $"{supplier.Code} connector is not configured.", null);
        }

        var importType = supplier.Code.ToUpperInvariant() switch
        {
            WpsSupplierCode => "WpsFitment",
            Turn14SupplierCode => "Turn14Fitment",
            PartsUnlimitedSupplierCode => "PartsUnlimitedFitment",
            _ => null
        };
        if (importType is null)
        {
            return new SupplierItemFitmentQueueResult(false, $"Fitment fetching is not supported for supplier {supplier.Code}.", null);
        }

        var existingFitmentRecordCount = await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .CountAsync(x =>
                x.SupplierProductId == supplierProduct.Id ||
                (x.SupplierId == supplierProduct.SupplierId && x.SupplierSku == supplierProduct.SupplierSku),
                cancellationToken);
        if (existingFitmentRecordCount > 0)
        {
            var recordLabel = existingFitmentRecordCount == 1 ? "record" : "records";
            return new SupplierItemFitmentQueueResult(
                false,
                $"{supplier.Code} SKU {supplierProduct.SupplierSku} already has {existingFitmentRecordCount:N0} fitment {recordLabel}.",
                null);
        }

        var missingFields = RequiredFitmentFields(configuration).ToArray();
        var now = dateTimeProvider.UtcNow;
        var source = configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl;
        var status = missingFields.Length == 0 ? "Queued" : "Blocked";
        var message = missingFields.Length == 0
            ? $"{supplier.Code} fitment fetch queued for SKU {supplierProduct.SupplierSku}."
            : $"{supplier.Code} fitment fetch blocked for SKU {supplierProduct.SupplierSku}. Missing required settings: {string.Join(", ", missingFields)}.";

        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = importType,
            Status = status,
            RequestedByPlatformUserId = platformUserId,
            RequestedAtUtc = now,
            Source = source,
            ProgressProcessed = 0,
            ProgressTotal = 1,
            ParametersJson = JsonSerializer.Serialize(new
            {
                SupplierCode = supplier.Code,
                Sku = supplierProduct.SupplierSku,
                ImportMode = "ItemSearch",
                MaxSkus = 1,
                FitmentLimit = configuration.FitmentScheduleFitmentLimit,
                DelayMilliseconds = ClampPositive(configuration.FitmentScheduleDelayMilliseconds, 0, 5000),
                BaseUrl = source,
                ExcludeNla = false
            }),
            Message = message
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);

        AddAuditEvent("SupplierItemFitmentFetchRequested", platformUserId, new
        {
            supplier.Code,
            supplierProduct.SupplierSku,
            SupplierProductId = supplierProduct.Id,
            importType,
            status
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new SupplierItemFitmentQueueResult(status == "Queued", message, importRun.Id);
    }

    private async Task<SupplierConnectorConfiguration> EnsureWpsConfigurationAsync(CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Code == WpsSupplierCode, cancellationToken);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = WpsSupplierName,
                Code = WpsSupplierCode,
                ConnectorKey = WpsConnectorKey,
                IsActive = true
            };
            dbContext.Suppliers.Add(supplier);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == WpsConnectorKey, cancellationToken);

        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = WpsConnectorKey,
            DisplayName = "WPS",
            AuthMode = "DataDepotDealerLoginAndApiKey",
            IsEnabled = false,
            ImportMasterFileOnSchedule = false,
            MasterFileImportMode = "Manual",
            BaseApiUrl = WpsDefaultBaseApiUrl,
            MasterFileUrl = WpsDefaultMasterFileUrl,
            MasterFileScheduleCadenceMinutes = 1440,
            ImportFitmentOnSchedule = false,
            FitmentScheduleCadenceMinutes = 1440,
            FitmentScheduleDelayMilliseconds = 250,
            FitmentSourceBaseUrl = DefaultFitmentSourceBaseUrl,
            ImportMediaOnSchedule = false,
            MediaScheduleCadenceMinutes = 1440,
            MediaScheduleDelayMilliseconds = 750
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return configuration;
    }

    private async Task<SupplierConnectorConfiguration> EnsureTurn14ConfigurationAsync(CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Code == Turn14SupplierCode, cancellationToken);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = Turn14SupplierName,
                Code = Turn14SupplierCode,
                ConnectorKey = Turn14ConnectorKey,
                IsActive = true
            };
            dbContext.Suppliers.Add(supplier);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == Turn14ConnectorKey, cancellationToken);

        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = Turn14ConnectorKey,
            DisplayName = "Turn14",
            AuthMode = "CookieLogin",
            IsEnabled = false,
            ImportMasterFileOnSchedule = false,
            MasterFileImportMode = "Manual",
            BaseApiUrl = Turn14DefaultBaseUrl,
            MasterFileUrl = "POST /export.php stockExport=items",
            MasterFileScheduleCadenceMinutes = 1440,
            ImportFitmentOnSchedule = false,
            FitmentScheduleCadenceMinutes = 1440,
            FitmentScheduleDelayMilliseconds = 250,
            FitmentSourceBaseUrl = DefaultFitmentSourceBaseUrl
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return configuration;
    }

    private async Task<SupplierConnectorConfiguration> EnsurePartsUnlimitedConfigurationAsync(CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Code == PartsUnlimitedSupplierCode, cancellationToken);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = PartsUnlimitedSupplierName,
                Code = PartsUnlimitedSupplierCode,
                ConnectorKey = PartsUnlimitedConnectorKey,
                IsActive = true
            };
            dbContext.Suppliers.Add(supplier);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == PartsUnlimitedConnectorKey, cancellationToken);

        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = PartsUnlimitedConnectorKey,
            DisplayName = "Parts Unlimited",
            AuthMode = "ApiKeyHeader",
            IsEnabled = false,
            ImportMasterFileOnSchedule = false,
            MasterFileImportMode = "Manual",
            BaseApiUrl = PartsUnlimitedDefaultBaseApiUrl,
            MasterFileUrl = PartsUnlimitedDefaultBundlePath,
            MasterFileScheduleCadenceMinutes = 1440,
            ImportFitmentOnSchedule = false,
            FitmentScheduleCadenceMinutes = 1440,
            FitmentScheduleDelayMilliseconds = 250,
            FitmentSourceBaseUrl = DefaultFitmentSourceBaseUrl
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        await dbContext.SaveChangesAsync(cancellationToken);

        return configuration;
    }

    private async Task<WpsConnectorPage> BuildPageAsync(Guid configurationId, CancellationToken cancellationToken)
    {
        var configuration = await dbContext.SupplierConnectorConfigurations
            .AsNoTracking()
            .SingleAsync(x => x.Id == configurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);
        var importRunRows = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .Where(x => x.SupplierConnectorConfigurationId == configuration.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        var importRuns = importRunRows
            .Select(x => new SupplierConnectorImportRunRow(
                x.Id,
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.Message,
                ImportProgressPercent(x.Status, x.ProgressProcessed, x.ProgressTotal),
                x.ProgressProcessed,
                x.ProgressTotal))
            .ToList();
        var lastSuccessfulMasterImportCompletedAtUtc = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .Where(x =>
                x.SupplierConnectorConfigurationId == configuration.Id &&
                x.ImportType == "WpsMasterFile" &&
                x.Status == "Completed" &&
                x.CompletedAtUtc != null)
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var masterFileRemoteStatus = await GetMasterFileRemoteStatusAsync(
            configuration.MasterFileUrl ?? WpsDefaultMasterFileUrl,
            lastSuccessfulMasterImportCompletedAtUtc,
            cancellationToken);
        var databaseMetrics = await GetDatabaseMetricsAsync(supplier.Id, cancellationToken);

        return new WpsConnectorPage(
            new WpsConnectorSettingsRequest(
                configuration.IsEnabled,
                configuration.BaseApiUrl ?? WpsDefaultBaseApiUrl,
                configuration.MasterFileUrl ?? WpsDefaultMasterFileUrl,
                configuration.DealerAccountNumber ?? string.Empty,
                string.Empty,
                configuration.ApiKey ?? string.Empty,
                configuration.ImportMasterFileOnSchedule,
                configuration.MasterFileImportMode ?? "Manual",
                MinutesToHours(configuration.MasterFileScheduleCadenceMinutes),
                configuration.MasterFileScheduleMaxItems,
                configuration.ImportFitmentOnSchedule,
                MinutesToHours(configuration.FitmentScheduleCadenceMinutes),
                configuration.FitmentScheduleMaxSkus,
                configuration.FitmentScheduleFitmentLimit,
                configuration.FitmentScheduleDelayMilliseconds < 0 ? 250 : configuration.FitmentScheduleDelayMilliseconds,
                configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl),
            new WpsConnectorStatus(
                configuration.Id,
                supplier.Id,
                supplier.Name,
                configuration.ConnectorKey,
                RequiredConnectionFields(configuration).Any() == false,
                configuration.IsEnabled,
                configuration.LastConnectionTestAtUtc,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage),
            masterFileRemoteStatus,
            databaseMetrics,
            importRuns);
    }

    private async Task<Turn14ConnectorPage> BuildTurn14PageAsync(Guid configurationId, CancellationToken cancellationToken)
    {
        var configuration = await dbContext.SupplierConnectorConfigurations
            .AsNoTracking()
            .SingleAsync(x => x.Id == configurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);
        var importRunRows = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .Where(x => x.SupplierConnectorConfigurationId == configuration.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        var importRuns = importRunRows
            .Select(x => new SupplierConnectorImportRunRow(
                x.Id,
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.Message,
                ImportProgressPercent(x.Status, x.ProgressProcessed, x.ProgressTotal),
                x.ProgressProcessed,
                x.ProgressTotal))
            .ToList();
        var hasEnvironmentCredentials = HasTurn14EnvironmentCredentials();
        var secrets = Turn14ConnectorSecrets.FromConfiguration(configuration);
        var hasStoredCredentials = !string.IsNullOrWhiteSpace(configuration.Username) &&
            !string.IsNullOrWhiteSpace(configuration.ApiSecretProtected);
        var loadsheetFileStatus = await GetTurn14LoadsheetFileStatusAsync(configuration.Id, cancellationToken);
        var databaseMetrics = await GetDatabaseMetricsAsync(supplier.Id, cancellationToken);

        return new Turn14ConnectorPage(
            new Turn14ConnectorSettingsRequest(
                configuration.IsEnabled,
                configuration.BaseApiUrl ?? Turn14DefaultBaseUrl,
                configuration.Username ?? string.Empty,
                string.Empty,
                configuration.ApiKey ?? string.Empty,
                string.Empty,
                configuration.ImportMasterFileOnSchedule,
                configuration.MasterFileImportMode ?? "Manual",
                MinutesToHours(configuration.MasterFileScheduleCadenceMinutes),
                configuration.MasterFileScheduleMaxItems,
                configuration.ImportFitmentOnSchedule,
                MinutesToHours(configuration.FitmentScheduleCadenceMinutes),
                configuration.FitmentScheduleMaxSkus,
                configuration.FitmentScheduleFitmentLimit,
                configuration.FitmentScheduleDelayMilliseconds < 0 ? 250 : configuration.FitmentScheduleDelayMilliseconds,
                configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl,
                configuration.ImportMediaOnSchedule,
                MinutesToHours(configuration.MediaScheduleCadenceMinutes),
                configuration.MediaScheduleMaxItems,
                configuration.MediaScheduleDelayMilliseconds < 0 ? 750 : configuration.MediaScheduleDelayMilliseconds),
            new Turn14ConnectorStatus(
                configuration.Id,
                supplier.Id,
                supplier.Name,
                configuration.ConnectorKey,
                hasEnvironmentCredentials,
                hasStoredCredentials,
                secrets.HasApiCredentials,
                RequiredTurn14ConnectionFields(configuration).Any() == false,
                configuration.IsEnabled,
                configuration.LastConnectionTestAtUtc,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage),
            loadsheetFileStatus,
            databaseMetrics,
            importRuns);
    }

    private async Task<Turn14LoadsheetFileStatus> GetTurn14LoadsheetFileStatusAsync(Guid configurationId, CancellationToken cancellationToken)
    {
        var run = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .Where(x =>
                x.SupplierConnectorConfigurationId == configurationId &&
                x.ImportType == "Turn14ProductLoadsheet" &&
                x.Status == "Completed")
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return new Turn14LoadsheetFileStatus(null, null, null, null);
        }

        var metadata = ReadTurn14LoadsheetMetadata(run.ParametersJson);
        return new Turn14LoadsheetFileStatus(
            metadata.FileName,
            metadata.FileLastModifiedUtc,
            metadata.LastDownloadedAtUtc,
            run.CompletedAtUtc);
    }

    private async Task<PartsUnlimitedConnectorPage> BuildPartsUnlimitedPageAsync(Guid configurationId, CancellationToken cancellationToken)
    {
        var configuration = await dbContext.SupplierConnectorConfigurations
            .AsNoTracking()
            .SingleAsync(x => x.Id == configurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);
        var importRunRows = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .Where(x => x.SupplierConnectorConfigurationId == configuration.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        var importRuns = importRunRows
            .Select(x => new SupplierConnectorImportRunRow(
                x.Id,
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.Message,
                ImportProgressPercent(x.Status, x.ProgressProcessed, x.ProgressTotal),
                x.ProgressProcessed,
                x.ProgressTotal))
            .ToList();
        var partsUnlimitedOptions = PartsUnlimitedConnectorOptions.FromConfiguration(configuration);
        var databaseMetrics = await GetDatabaseMetricsAsync(supplier.Id, cancellationToken);

        return new PartsUnlimitedConnectorPage(
            new PartsUnlimitedConnectorSettingsRequest(
                configuration.IsEnabled,
                configuration.BaseApiUrl ?? PartsUnlimitedDefaultBaseApiUrl,
                configuration.ApiKey ?? string.Empty,
                partsUnlimitedOptions.DealerPortalUsername ?? string.Empty,
                string.Empty,
                partsUnlimitedOptions.DealerPortalDealerCode ?? string.Empty,
                partsUnlimitedOptions.BrandFileUrls ?? string.Empty,
                partsUnlimitedOptions.BrandFileMaxFiles,
                configuration.ImportMediaOnSchedule,
                MinutesToHours(configuration.MediaScheduleCadenceMinutes),
                configuration.MediaScheduleMaxItems,
                configuration.ImportMasterFileOnSchedule,
                configuration.MasterFileImportMode ?? "Manual",
                MinutesToHours(configuration.MasterFileScheduleCadenceMinutes),
                configuration.MasterFileScheduleMaxItems,
                configuration.ImportFitmentOnSchedule,
                MinutesToHours(configuration.FitmentScheduleCadenceMinutes),
                configuration.FitmentScheduleMaxSkus,
                configuration.FitmentScheduleFitmentLimit,
                configuration.FitmentScheduleDelayMilliseconds < 0 ? 250 : configuration.FitmentScheduleDelayMilliseconds,
                configuration.FitmentSourceBaseUrl ?? DefaultFitmentSourceBaseUrl),
            new PartsUnlimitedConnectorStatus(
                configuration.Id,
                supplier.Id,
                supplier.Name,
                configuration.ConnectorKey,
                RequiredPartsUnlimitedConnectionFields(configuration).Any() == false,
                configuration.IsEnabled,
                configuration.LastConnectionTestAtUtc,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage),
            new PartsUnlimitedBrandCacheStatus(
                partsUnlimitedOptions.BrandCacheRefreshedAtUtc,
                partsUnlimitedOptions.CachedBrands.Count,
                partsUnlimitedOptions.CachedBrands.Take(8).ToList()),
            databaseMetrics,
            importRuns);
    }

    private async Task<SupplierConnectorDatabaseMetrics> GetDatabaseMetricsAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var supplierProductCount = await dbContext.SupplierProducts
            .AsNoTracking()
            .LongCountAsync(x => x.SupplierId == supplierId, cancellationToken);
        var fitmentRecordCount = await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .LongCountAsync(x => x.SupplierId == supplierId, cancellationToken);

        return new SupplierConnectorDatabaseMetrics(supplierProductCount, fitmentRecordCount);
    }

    private async Task<WpsMasterFileRemoteStatus> GetMasterFileRemoteStatusAsync(string sourceUrl, DateTimeOffset? lastSuccessfulImportCompletedAtUtc, CancellationToken cancellationToken)
    {
        var checkedAtUtc = dateTimeProvider.UtcNow;
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return new WpsMasterFileRemoteStatus(
                false,
                checkedAtUtc,
                null,
                lastSuccessfulImportCompletedAtUtc,
                false,
                null,
                null,
                "Master Item List URL is not configured.");
        }

        try
        {
            var client = httpClientFactory.CreateClient("WpsDataDepot");
            using var request = new HttpRequestMessage(HttpMethod.Head, sourceUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var lastModifiedUtc = response.Content.Headers.LastModified;
            var isRemoteNewerThanLastImport = lastModifiedUtc is not null &&
                (lastSuccessfulImportCompletedAtUtc is null || lastModifiedUtc.Value > lastSuccessfulImportCompletedAtUtc.Value);

            return new WpsMasterFileRemoteStatus(
                response.IsSuccessStatusCode,
                checkedAtUtc,
                lastModifiedUtc,
                lastSuccessfulImportCompletedAtUtc,
                isRemoteNewerThanLastImport,
                response.Content.Headers.ContentLength,
                response.Headers.ETag?.Tag,
                response.IsSuccessStatusCode
                    ? null
                    : $"HEAD request failed with HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return new WpsMasterFileRemoteStatus(
                false,
                checkedAtUtc,
                null,
                lastSuccessfulImportCompletedAtUtc,
                false,
                null,
                null,
                exception.Message);
        }
    }

    private async Task<GlobalSchedulerEventRow> BuildSchedulerRowAsync(
        Guid configurationId,
        string name,
        string owner,
        string eventType,
        bool isEnabled,
        int cadenceMinutes,
        DateTimeOffset? lastQueuedAtUtc,
        string configuratorController,
        string configuratorAction,
        CancellationToken cancellationToken)
    {
        var lastRun = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .Where(x => x.SupplierConnectorConfigurationId == configurationId && x.ImportType == eventType)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => new
            {
                x.Status,
                x.CompletedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
        DateTimeOffset? nextDueAtUtc = null;
        if (isEnabled)
        {
            nextDueAtUtc = lastQueuedAtUtc is null
                ? dateTimeProvider.UtcNow
                : lastQueuedAtUtc.Value.AddMinutes(Math.Max(60, cadenceMinutes));
        }

        return new GlobalSchedulerEventRow(
            $"{configurationId:N}:{eventType}",
            name,
            owner,
            eventType,
            isEnabled,
            cadenceMinutes,
            lastQueuedAtUtc,
            nextDueAtUtc,
            lastRun?.Status,
            lastRun?.CompletedAtUtc,
            configuratorController,
            configuratorAction);
    }

    private void AddAuditEvent(string eventType, string? platformUserId, object payload)
    {
        dbContext.PlatformEvents.Add(new()
        {
            ActorPlatformUserId = platformUserId,
            EventType = eventType,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            PayloadJson = JsonSerializer.Serialize(payload),
            CorrelationId = Guid.NewGuid().ToString("N")
        });

        dbContext.PlatformAuditEvents.Add(new()
        {
            ActorPlatformUserId = platformUserId,
            Action = eventType,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            NewValuesJson = JsonSerializer.Serialize(payload)
        });
    }

    private static IEnumerable<string> RequiredConnectionFields(SupplierConnectorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            yield return "API key";
        }

    }

    private static IEnumerable<string> RequiredMasterFileFields(SupplierConnectorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.MasterFileUrl))
        {
            yield return "Master Item List URL";
        }
    }

    private static IEnumerable<string> RequiredFitmentFields(SupplierConnectorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.FitmentSourceBaseUrl))
        {
            yield return "Fitment source URL";
        }
    }

    private static IEnumerable<string> RequiredTurn14ConnectionFields(SupplierConnectorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.BaseApiUrl))
        {
            yield return "Base URL";
        }

        var secrets = Turn14ConnectorSecrets.FromConfiguration(configuration);
        if (!secrets.HasWebCredentials)
        {
            yield return "TURN14_USERNAME and TURN14_PASSWORD";
        }
    }

    private static IEnumerable<string> RequiredPartsUnlimitedConnectionFields(SupplierConnectorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.BaseApiUrl))
        {
            yield return "Base API URL";
        }

        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            yield return "API key";
        }
    }

    private static IEnumerable<string> RequiredPartsUnlimitedDealerPortalFields(PartsUnlimitedConnectorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DealerPortalUsername))
        {
            yield return "Dealer Portal User ID";
        }

        if (string.IsNullOrWhiteSpace(options.DealerPortalPassword))
        {
            yield return "Dealer Portal Password";
        }

        if (string.IsNullOrWhiteSpace(options.DealerPortalDealerCode))
        {
            yield return "Dealer Number";
        }
    }

    private static IEnumerable<string> RequiredPartsUnlimitedBrandImageFields(SupplierConnectorConfiguration configuration, PartsUnlimitedConnectorOptions options)
    {
        foreach (var field in RequiredPartsUnlimitedConnectionFields(configuration))
        {
            yield return field;
        }

        var hasConfiguredBrandFileUrls = !string.IsNullOrWhiteSpace(options.BrandFileUrls);
        var hasCachedBrandFileIds = options.CachedBrands.Any(x => !string.IsNullOrWhiteSpace(x.BrandId));
        if (!hasConfiguredBrandFileUrls && !hasCachedBrandFileIds)
        {
            yield return "brand file URL(s) or refreshed brand cache";
            yield break;
        }

        if (!hasConfiguredBrandFileUrls && hasCachedBrandFileIds)
        {
            foreach (var field in RequiredPartsUnlimitedDealerPortalFields(options))
            {
                yield return field;
            }
        }
    }

    private static async Task<IReadOnlyCollection<PartsUnlimitedCachedBrandRow>> FetchPartsUnlimitedBrandsAsync(PartsUnlimitedConnectorOptions options, CancellationToken cancellationToken)
    {
        var cookieJar = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieJar,
            UseCookies = true,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using var loginRequest = new HttpRequestMessage(HttpMethod.Put, $"https://dealer.parts-unlimited.com/api/login?t={timestamp}");
        loginRequest.Headers.TryAddWithoutValidation("Origin", "https://dealer.parts-unlimited.com");
        loginRequest.Headers.TryAddWithoutValidation("Referer", "https://dealer.parts-unlimited.com/login");
        loginRequest.Content = JsonContent(new
        {
            username = options.DealerPortalUsername,
            password = options.DealerPortalPassword,
            dealerCode = options.DealerPortalDealerCode
        });
        using var loginResponse = await client.SendAsync(loginRequest, cancellationToken);
        loginResponse.EnsureSuccessStatusCode();

        using var searchRequest = new HttpRequestMessage(HttpMethod.Post, "https://dealer.parts-unlimited.com/api/parts/search");
        searchRequest.Headers.TryAddWithoutValidation("Origin", "https://dealer.parts-unlimited.com");
        searchRequest.Headers.TryAddWithoutValidation("Referer", "https://dealer.parts-unlimited.com/reptools/brand-report");
        searchRequest.Content = JsonContent(new
        {
            filterAggregations = new[] { "brand" },
            pagination = new { limit = 0, offset = 0 },
            partActiveScope = "ACTIVE_ONLY"
        });
        using var searchResponse = await client.SendAsync(searchRequest, cancellationToken);
        searchResponse.EnsureSuccessStatusCode();
        await using var stream = await searchResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParsePartsUnlimitedBrandRows(document.RootElement);
    }

    private static StringContent JsonContent<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");
    }

    private static IReadOnlyCollection<PartsUnlimitedCachedBrandRow> ParsePartsUnlimitedBrandRows(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("filterOptions", out var filterOptions) ||
            filterOptions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var tier in filterOptions.EnumerateArray())
        {
            var tierName = ReadString(tier, "tierName");
            if (!string.Equals(tierName, "brand", StringComparison.OrdinalIgnoreCase) ||
                !tier.TryGetProperty("tierOptions", out var tierOptions) ||
                tierOptions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var brands = new List<PartsUnlimitedCachedBrandRow>();
            foreach (var option in tierOptions.EnumerateArray())
            {
                var displayName = ReadString(option, "displayName");
                if (displayName is null)
                {
                    continue;
                }

                var brandId = string.Empty;
                if (option.TryGetProperty("match", out var match) && match.ValueKind == JsonValueKind.Object)
                {
                    brandId = ReadString(match, "value") ?? string.Empty;
                }

                brands.Add(new PartsUnlimitedCachedBrandRow(
                    brandId,
                    displayName,
                    ReadInt(option, "count") ?? 0,
                    ReadInt(option, "filteredCount") ?? 0));
            }

            return brands
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return [];
    }

    private static string LimitedImportMessage(int? maxItems)
    {
        return maxItems is null
            ? "WPS Master Item List import request queued for connector worker execution."
            : $"WPS Master Item List import request queued for first {maxItems.Value} items.";
    }

    private static string Turn14LimitedImportMessage(int? maxItems)
    {
        return maxItems is null
            ? "Turn14 product loadsheet import request queued for connector worker execution."
            : $"Turn14 product loadsheet import request queued for first {maxItems.Value} rows.";
    }

    private static string Turn14LimitedFitmentMessage(int? maxSkus)
    {
        return maxSkus is null
            ? "Turn14 fitment import request queued for connector worker execution."
            : $"Turn14 fitment import request queued for first {maxSkus.Value} active SKUs.";
    }

    private static string Turn14LimitedMediaMessage(int? maxItems)
    {
        return maxItems is null
            ? "Turn14 media enrichment request queued for connector worker execution."
            : $"Turn14 media enrichment request queued for first {maxItems.Value} products missing media.";
    }

    private static string PartsUnlimitedLimitedImportMessage(int? maxItems)
    {
        return maxItems is null
            ? "Parts Unlimited bundle import request queued for connector worker execution."
            : $"Parts Unlimited bundle import request queued for first {maxItems.Value} parts.";
    }

    private static string PartsUnlimitedLimitedFitmentMessage(int? maxSkus)
    {
        return maxSkus is null
            ? "Parts Unlimited fitment import request queued for connector worker execution."
            : $"Parts Unlimited fitment import request queued for first {maxSkus.Value} active SKUs.";
    }

    private static string LimitedFitmentMessage(int? maxSkus)
    {
        return maxSkus is null
            ? "WPS fitment import request queued for connector worker execution."
            : $"WPS fitment import request queued for first {maxSkus.Value} active SKUs.";
    }

    private static int ImportProgressPercent(string status, int processed, int? total)
    {
        if (total is > 0)
        {
            return Math.Clamp((int)Math.Floor(processed * 100m / total.Value), 0, 100);
        }

        return status switch
        {
            "Queued" => 10,
            "Running" => 50,
            "Completed" => 100,
            "Failed" => 100,
            "Blocked" => 100,
            _ => 0
        };
    }

    private static int ClampPositive(int value, int minimum, int maximum)
    {
        return Math.Min(Math.Max(value, minimum), maximum);
    }

    private static int HoursToMinutes(int value)
    {
        return ClampPositive(value, 1, 168) * 60;
    }

    private static int MinutesToHours(int value)
    {
        return Math.Max(1, (int)Math.Ceiling((value <= 0 ? 1440 : value) / 60m));
    }

    private static int? PositiveOrNull(int? value)
    {
        return value is > 0 ? value : null;
    }

    private static int? EffectiveManualLimit(string? importMode, int? value)
    {
        return string.Equals(importMode, "LimitedTest", StringComparison.OrdinalIgnoreCase)
            ? PositiveOrNull(value)
            : null;
    }

    private static Turn14LoadsheetMetadata ReadTurn14LoadsheetMetadata(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return new Turn14LoadsheetMetadata(null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(parametersJson);
            var root = document.RootElement;
            return new Turn14LoadsheetMetadata(
                ReadString(root, "ProductLoadsheetFileName"),
                ReadDateTimeOffset(root, "ProductLoadsheetLastModifiedUtc"),
                ReadDateTimeOffset(root, "ProductLoadsheetDownloadedAtUtc"));
        }
        catch (JsonException)
        {
            return new Turn14LoadsheetMetadata(null, null, null);
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? Clean(value.GetString())
            : null;
    }

    private static int? ReadInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), out var parsed)
                ? parsed
                : null;
    }

    private static string Required(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.")
            : value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasTurn14EnvironmentCredentials()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TURN14_USERNAME")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TURN14_PASSWORD"));
    }

    private sealed record Turn14LoadsheetMetadata(
        string? FileName,
        DateTimeOffset? FileLastModifiedUtc,
        DateTimeOffset? LastDownloadedAtUtc);

    private sealed record PartsUnlimitedConnectorOptions(
        bool ImportBrandFilesWithBundle,
        string? BrandFileUrls,
        int? BrandFileMaxFiles,
        string? DealerPortalUsername,
        string? DealerPortalPassword,
        string? DealerPortalDealerCode,
        DateTimeOffset? BrandCacheRefreshedAtUtc,
        IReadOnlyCollection<PartsUnlimitedCachedBrandRow> CachedBrands)
    {
        public static PartsUnlimitedConnectorOptions FromConfiguration(SupplierConnectorConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.ApiSecretProtected))
            {
                return Empty;
            }

            try
            {
                return Normalize(JsonSerializer.Deserialize<PartsUnlimitedConnectorOptions>(configuration.ApiSecretProtected));
            }
            catch (JsonException)
            {
                return Empty;
            }
        }

        private static PartsUnlimitedConnectorOptions Empty => new(false, null, null, null, null, null, null, []);

        private static PartsUnlimitedConnectorOptions Normalize(PartsUnlimitedConnectorOptions? options)
        {
            return options is null
                ? Empty
                : options with
                {
                    CachedBrands = options.CachedBrands ?? []
                };
        }
    }
}
