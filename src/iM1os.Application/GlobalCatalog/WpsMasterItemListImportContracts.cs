namespace iM1os.Application.GlobalCatalog;

public sealed record WpsMasterItemListImportRequest(
    Guid ImportRunId,
    int? MaxItems);

public sealed record WpsMasterItemListImportResult(
    Guid ImportRunId,
    int Processed,
    int CreatedGlobalProducts,
    int UpdatedGlobalProducts,
    int CreatedSupplierProducts,
    int UpdatedSupplierProducts,
    int UpsertedPrices);

public interface IWpsMasterItemListImportService
{
    Task<WpsMasterItemListImportResult> ImportAsync(WpsMasterItemListImportRequest request, CancellationToken cancellationToken);
}

public sealed record Turn14ProductLoadsheetImportRequest(
    Guid ImportRunId,
    int? MaxItems);

public sealed record Turn14ProductLoadsheetImportResult(
    Guid ImportRunId,
    int Processed,
    int CreatedGlobalProducts,
    int UpdatedGlobalProducts,
    int CreatedSupplierProducts,
    int UpdatedSupplierProducts,
    int UpsertedPrices);

public interface ITurn14ProductLoadsheetImportService
{
    Task<Turn14ProductLoadsheetImportResult> ImportAsync(Turn14ProductLoadsheetImportRequest request, CancellationToken cancellationToken);
}

public sealed record Turn14MediaEnrichmentRunRequest(
    Guid ImportRunId,
    int? MaxItems,
    int DelayMilliseconds);

public sealed record Turn14MediaEnrichmentRunResult(
    Guid ImportRunId,
    int Processed,
    int UpdatedProducts,
    int SkippedProducts,
    bool StoppedForRateLimit);

public interface ITurn14MediaEnrichmentService
{
    Task<Turn14MediaEnrichmentRunResult> ImportAsync(Turn14MediaEnrichmentRunRequest request, CancellationToken cancellationToken);
}

public sealed record PartsUnlimitedBundleImportRequest(
    Guid ImportRunId,
    int? MaxItems);

public sealed record PartsUnlimitedBundleImportResult(
    Guid ImportRunId,
    int Processed,
    int CreatedGlobalProducts,
    int UpdatedGlobalProducts,
    int CreatedSupplierProducts,
    int UpdatedSupplierProducts,
    int UpsertedPrices,
    int BrandFilesProcessed,
    int BrandImageRowsProcessed,
    int BrandImagesUpdated,
    int BrandImageRowsUnmatched);

public interface IPartsUnlimitedBundleImportService
{
    Task<PartsUnlimitedBundleImportResult> ImportAsync(PartsUnlimitedBundleImportRequest request, CancellationToken cancellationToken);
}

public sealed record PartsUnlimitedBrandImageImportRequest(
    Guid ImportRunId,
    int? MaxFiles);

public sealed record PartsUnlimitedBrandImageImportResult(
    Guid ImportRunId,
    int BrandFilesProcessed,
    int BrandImageRowsProcessed,
    int BrandImagesUpdated,
    int BrandImageRowsUnmatched);

public interface IPartsUnlimitedBrandImageImportService
{
    Task<PartsUnlimitedBrandImageImportResult> ImportAsync(PartsUnlimitedBrandImageImportRequest request, CancellationToken cancellationToken);
}
