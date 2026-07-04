namespace iM1os.Application.CompanySuppliers;

public sealed record CompanyWpsConnectorPage(
    Guid OrganizationId,
    CompanyWpsConnectorSettingsRequest Settings,
    CompanyWpsConnectorStatus Status,
    CompanyWpsPriceFileStatus PriceFileStatus,
    IReadOnlyCollection<CompanySupplierImportRunRow> RecentImportRuns);

public sealed record CompanySupplierConnectorPage(
    Guid OrganizationId,
    string SupplierCode,
    string SupplierName,
    string ConnectorTitle,
    string ConnectorDescription,
    string PricingDescription,
    string SaveAction,
    string SyncAction,
    string ApiKeyLabel,
    string ApiSecretLabel,
    string BaseApiUrlPlaceholder,
    CompanySupplierConnectorSettingsRequest Settings,
    CompanySupplierConnectorStatus Status,
    CompanySupplierPriceSyncStatus PriceSyncStatus,
    IReadOnlyCollection<CompanySupplierImportRunRow> RecentImportRuns);

public sealed record CompanyWpsConnectorSettingsRequest(
    bool IsEnabled,
    string BaseApiUrl,
    string DealerAccountNumber,
    string Username,
    string ApiKey,
    string ApiSecret,
    bool SyncDealerPricingOnSchedule,
    int DealerPricingScheduleIntervalDays,
    int? DealerPricingScheduleMaxItems);

public sealed record CompanySupplierConnectorSettingsRequest(
    bool IsEnabled,
    string BaseApiUrl,
    string DealerAccountNumber,
    string Username,
    string ApiKey,
    string ApiSecret,
    bool SyncDealerPricingOnSchedule,
    int DealerPricingScheduleIntervalDays,
    int? DealerPricingScheduleMaxItems);

public sealed record CompanyWpsDealerPricingSyncRequest(
    int? MaxItems);

public sealed record CompanySupplierDealerPricingSyncRequest(
    int? MaxItems);

public sealed record CompanyWpsConnectorStatus(
    Guid ConnectorConfigurationId,
    Guid SupplierId,
    string SupplierName,
    string ConnectorKey,
    bool IsConfigured,
    bool IsEnabled,
    bool SyncDealerPricingOnSchedule,
    DateTimeOffset? LastDealerPricingScheduledAtUtc,
    DateTimeOffset? LastConnectionTestAtUtc,
    string? LastConnectionStatus,
    string? LastConnectionMessage);

public sealed record CompanySupplierConnectorStatus(
    Guid ConnectorConfigurationId,
    Guid SupplierId,
    string SupplierName,
    string ConnectorKey,
    bool IsConfigured,
    bool IsEnabled,
    bool SyncDealerPricingOnSchedule,
    DateTimeOffset? LastDealerPricingScheduledAtUtc,
    DateTimeOffset? LastConnectionTestAtUtc,
    string? LastConnectionStatus,
    string? LastConnectionMessage);

public sealed record CompanyWpsPriceFileStatus(
    DateTimeOffset? PriceFileLastModifiedUtc,
    DateTimeOffset? LastDownloadedAtUtc,
    DateTimeOffset? LastAppliedAtUtc,
    string? Source,
    string? Message);

public sealed record CompanySupplierPriceSyncStatus(
    DateTimeOffset? PriceFileLastModifiedUtc,
    DateTimeOffset? LastDownloadedAtUtc,
    DateTimeOffset? LastAppliedAtUtc,
    string? Source,
    string? Message);

public sealed record CompanySupplierImportRunRow(
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
    int? ProgressTotal,
    string? ParametersJson = null);
