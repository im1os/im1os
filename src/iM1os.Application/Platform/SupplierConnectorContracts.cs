namespace iM1os.Application.Platform;

public sealed record WpsConnectorPage(
    WpsConnectorSettingsRequest Settings,
    WpsConnectorStatus Status,
    WpsMasterFileRemoteStatus MasterFileRemoteStatus,
    SupplierConnectorDatabaseMetrics DatabaseMetrics,
    IReadOnlyCollection<SupplierConnectorImportRunRow> RecentImportRuns);

public sealed record Turn14ConnectorPage(
    Turn14ConnectorSettingsRequest Settings,
    Turn14ConnectorStatus Status,
    Turn14LoadsheetFileStatus ProductLoadsheetStatus,
    SupplierConnectorDatabaseMetrics DatabaseMetrics,
    IReadOnlyCollection<SupplierConnectorImportRunRow> RecentImportRuns);

public sealed record PartsUnlimitedConnectorPage(
    PartsUnlimitedConnectorSettingsRequest Settings,
    PartsUnlimitedConnectorStatus Status,
    PartsUnlimitedBrandCacheStatus BrandCacheStatus,
    SupplierConnectorDatabaseMetrics DatabaseMetrics,
    IReadOnlyCollection<SupplierConnectorImportRunRow> RecentImportRuns);

public sealed record SupplierConnectorDatabaseMetrics(
    long SupplierProductCount,
    long FitmentRecordCount);

public sealed record GlobalSchedulerPage(
    IReadOnlyCollection<GlobalSchedulerEventRow> Events);

public sealed record GlobalSchedulerEventRow(
    string SchedulerKey,
    string Name,
    string Owner,
    string EventType,
    bool IsEnabled,
    int CadenceMinutes,
    DateTimeOffset? LastQueuedAtUtc,
    DateTimeOffset? NextDueAtUtc,
    string? LastRunStatus,
    DateTimeOffset? LastRunCompletedAtUtc,
    string ConfiguratorController,
    string ConfiguratorAction);

public sealed record WpsConnectorSettingsRequest(
    bool IsEnabled,
    string BaseApiUrl,
    string MasterFileUrl,
    string DealerAccountNumber,
    string DataDepotPassword,
    string ApiKey,
    bool ImportMasterFileOnSchedule,
    string MasterFileImportMode,
    int MasterFileScheduleIntervalDays,
    int? MasterFileScheduleMaxItems,
    bool ImportFitmentOnSchedule,
    int FitmentScheduleIntervalDays,
    int? FitmentScheduleMaxSkus,
    int? FitmentScheduleFitmentLimit,
    int FitmentScheduleDelayMilliseconds,
    string FitmentSourceBaseUrl);

public sealed record WpsMasterFileImportRequest(
    bool ImportProducts,
    bool ImportSupplierPricing,
    bool ImportFitment,
    bool UpdateExistingProducts,
    bool CreateMissingProducts,
    string ImportMode,
    int? MaxItems);

public sealed record WpsFitmentImportRequest(
    string ImportMode,
    int? MaxSkus,
    int? FitmentLimit,
    int DelayMilliseconds);

public sealed record Turn14ConnectorSettingsRequest(
    bool IsEnabled,
    string BaseUrl,
    string Username,
    string Password,
    string ApiClientId,
    string ApiClientSecret,
    bool ImportMasterFileOnSchedule,
    string MasterFileImportMode,
    int MasterFileScheduleIntervalDays,
    int? MasterFileScheduleMaxItems,
    bool ImportFitmentOnSchedule,
    int FitmentScheduleIntervalDays,
    int? FitmentScheduleMaxSkus,
    int? FitmentScheduleFitmentLimit,
    int FitmentScheduleDelayMilliseconds,
    string FitmentSourceBaseUrl,
    bool ImportMediaOnSchedule,
    int MediaScheduleIntervalDays,
    int? MediaScheduleMaxItems,
    int MediaScheduleDelayMilliseconds);

public sealed record Turn14MasterFileImportRequest(
    bool ImportProducts,
    bool ImportSupplierPricing,
    bool UpdateExistingProducts,
    bool CreateMissingProducts,
    string ImportMode,
    int? MaxItems);

public sealed record Turn14FitmentImportRequest(
    string ImportMode,
    int? MaxSkus,
    int? FitmentLimit,
    int DelayMilliseconds);

public sealed record Turn14MediaEnrichmentImportRequest(
    string ImportMode,
    int? MaxItems,
    int DelayMilliseconds);

public sealed record PartsUnlimitedConnectorSettingsRequest(
    bool IsEnabled,
    string BaseApiUrl,
    string ApiKey,
    string DealerPortalUsername,
    string DealerPortalPassword,
    string DealerPortalDealerCode,
    string BrandFileUrls,
    int? BrandFileMaxFiles,
    bool ImportBrandImagesOnSchedule,
    int BrandImagesScheduleIntervalDays,
    int? BrandImagesScheduleMaxFiles,
    bool ImportMasterFileOnSchedule,
    string MasterFileImportMode,
    int MasterFileScheduleIntervalDays,
    int? MasterFileScheduleMaxItems,
    bool ImportFitmentOnSchedule,
    int FitmentScheduleIntervalDays,
    int? FitmentScheduleMaxSkus,
    int? FitmentScheduleFitmentLimit,
    int FitmentScheduleDelayMilliseconds,
    string FitmentSourceBaseUrl);

public sealed record PartsUnlimitedMasterFileImportRequest(
    bool ImportProducts,
    bool ImportSupplierPricing,
    bool UpdateExistingProducts,
    bool CreateMissingProducts,
    string ImportMode,
    int? MaxItems);

public sealed record PartsUnlimitedBrandImagesImportRequest(
    string ImportMode,
    int? MaxFiles);

public sealed record PartsUnlimitedFitmentImportRequest(
    string ImportMode,
    int? MaxSkus,
    int? FitmentLimit,
    int DelayMilliseconds);

public sealed record SupplierItemFitmentQueueResult(
    bool Queued,
    string Message,
    Guid? ImportRunId);

public sealed record WpsConnectorStatus(
    Guid ConnectorConfigurationId,
    Guid SupplierId,
    string SupplierName,
    string ConnectorKey,
    bool IsConfigured,
    bool IsEnabled,
    DateTimeOffset? LastConnectionTestAtUtc,
    string? LastConnectionStatus,
    string? LastConnectionMessage);

public sealed record Turn14ConnectorStatus(
    Guid ConnectorConfigurationId,
    Guid SupplierId,
    string SupplierName,
    string ConnectorKey,
    bool HasEnvironmentCredentials,
    bool HasStoredCredentials,
    bool HasApiCredentials,
    bool IsConfigured,
    bool IsEnabled,
    DateTimeOffset? LastConnectionTestAtUtc,
    string? LastConnectionStatus,
    string? LastConnectionMessage);

public sealed record Turn14LoadsheetFileStatus(
    string? FileName,
    DateTimeOffset? FileLastModifiedUtc,
    DateTimeOffset? LastDownloadedAtUtc,
    DateTimeOffset? LastAppliedAtUtc);

public sealed record PartsUnlimitedConnectorStatus(
    Guid ConnectorConfigurationId,
    Guid SupplierId,
    string SupplierName,
    string ConnectorKey,
    bool IsConfigured,
    bool IsEnabled,
    DateTimeOffset? LastConnectionTestAtUtc,
    string? LastConnectionStatus,
    string? LastConnectionMessage);

public sealed record PartsUnlimitedBrandCacheStatus(
    DateTimeOffset? CachedAtUtc,
    int BrandCount,
    IReadOnlyCollection<PartsUnlimitedCachedBrandRow> SampleBrands);

public sealed record PartsUnlimitedCachedBrandRow(
    string BrandId,
    string DisplayName,
    int Count,
    int FilteredCount);

public sealed record WpsMasterFileRemoteStatus(
    bool IsAvailable,
    DateTimeOffset CheckedAtUtc,
    DateTimeOffset? LastModifiedUtc,
    DateTimeOffset? LastSuccessfulImportCompletedAtUtc,
    bool IsRemoteNewerThanLastImport,
    long? ContentLength,
    string? ETag,
    string? Message);

public sealed record SupplierConnectorImportRunRow(
    Guid Id,
    string ImportType,
    string Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Source,
    string? Message,
    int ProgressPercent,
    int ProgressProcessed,
    int? ProgressTotal);
