namespace iM1os.Application.GlobalCatalog;

public interface ICatalogNormalizationService
{
    Task<CatalogNormalizationResult> NormalizeAsync(CatalogNormalizationRequest request, CancellationToken cancellationToken);
}

public sealed record CatalogNormalizationRequest(
    Guid ImportRunId,
    int? MaxItems);

public sealed record CatalogNormalizationResult(
    Guid ImportRunId,
    int ProcessedSupplierProducts,
    int CreatedCanonicalItems,
    int UpdatedCanonicalItems,
    int UpsertedSupplierOffers,
    int AddedIdentifiers,
    int AddedSources,
    int AddedFitments,
    int Failed);
