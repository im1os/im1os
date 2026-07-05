namespace iM1os.Application.Platform;

public interface IPlatformSupplierConnectorService
{
    Task<WpsConnectorPage> GetWpsConnectorAsync(CancellationToken cancellationToken);

    Task<Turn14ConnectorPage> GetTurn14ConnectorAsync(CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> GetPartsUnlimitedConnectorAsync(CancellationToken cancellationToken);

    Task<GlobalSchedulerPage> GetGlobalSchedulerAsync(CancellationToken cancellationToken);

    Task<GlobalSchedulerPage> SaveGlobalCatalogParserBackfillSchedulerAsync(GlobalCatalogParserBackfillSettingsRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<GlobalSchedulerPage> QueueGlobalCatalogParserBackfillAsync(GlobalCatalogParserBackfillRunRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<GlobalSchedulerPage> SaveGlobalCatalogNormalizationSchedulerAsync(GlobalCatalogNormalizationSettingsRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<GlobalSchedulerPage> QueueGlobalCatalogNormalizationAsync(GlobalCatalogNormalizationRunRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<GlobalSchedulerPage> ResetGlobalCanonicalCatalogAsync(string? platformUserId, CancellationToken cancellationToken);

    Task<WpsConnectorPage> SaveWpsConnectorAsync(WpsConnectorSettingsRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<WpsConnectorPage> TestWpsConnectionAsync(string? platformUserId, CancellationToken cancellationToken);

    Task<Turn14ConnectorPage> TestTurn14ConnectionAsync(string? platformUserId, CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> TestPartsUnlimitedConnectionAsync(string? platformUserId, CancellationToken cancellationToken);

    Task<WpsConnectorPage> ImportWpsMasterFileAsync(WpsMasterFileImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<Turn14ConnectorPage> SaveTurn14ConnectorAsync(Turn14ConnectorSettingsRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<Turn14ConnectorPage> ImportTurn14MasterFileAsync(Turn14MasterFileImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<Turn14ConnectorPage> ImportTurn14FitmentAsync(Turn14FitmentImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<Turn14ConnectorPage> ImportTurn14MediaEnrichmentAsync(Turn14MediaEnrichmentImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> SavePartsUnlimitedConnectorAsync(PartsUnlimitedConnectorSettingsRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> RefreshPartsUnlimitedBrandCacheAsync(string? platformUserId, CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> ImportPartsUnlimitedMasterFileAsync(PartsUnlimitedMasterFileImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> ImportPartsUnlimitedBrandImagesAsync(PartsUnlimitedBrandImagesImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<PartsUnlimitedConnectorPage> ImportPartsUnlimitedFitmentAsync(PartsUnlimitedFitmentImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<WpsConnectorPage> ImportWpsFitmentAsync(WpsFitmentImportRequest request, string? platformUserId, CancellationToken cancellationToken);

    Task<SupplierItemFitmentQueueResult> QueueSupplierItemFitmentAsync(Guid supplierProductId, string? platformUserId, CancellationToken cancellationToken);
}
