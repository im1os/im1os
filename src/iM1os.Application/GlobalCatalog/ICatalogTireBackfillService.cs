namespace iM1os.Application.GlobalCatalog;

public interface ICatalogTireBackfillService
{
    Task<CatalogTireBackfillResult> BackfillAsync(CatalogTireBackfillRequest request, CancellationToken cancellationToken);
}

public sealed record CatalogTireBackfillRequest(
    Guid ImportRunId,
    int? MaxItems);

public sealed record CatalogTireBackfillResult(
    Guid ImportRunId,
    int Processed,
    int Updated,
    int TireProductsDetected,
    int NoTireDetected,
    int Failed);
