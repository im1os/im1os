namespace iM1os.Application.GlobalCatalog;

public sealed record ProductMatchRequest(
    Guid SupplierId,
    string SupplierSku,
    string? Upc,
    string? ManufacturerPartNumber,
    string? Brand,
    string? SupplierDescription);

public sealed record ProductMatchResult(
    ProductMatchType MatchType,
    Guid? GlobalProductId,
    decimal Confidence,
    bool RequiresManualReview,
    Guid? ReviewItemId,
    IReadOnlyCollection<ProductMatchCandidate> Candidates);

public sealed record ProductMatchCandidate(
    Guid GlobalProductId,
    string MatchReason,
    decimal Confidence,
    string Brand,
    string? ManufacturerPartNumber,
    string? Upc,
    string Description);

public enum ProductMatchType
{
    SupplierSkuMapping,
    Upc,
    ManufacturerPartNumber,
    BrandAndPartNumber,
    ManualReview
}
