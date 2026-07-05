namespace iM1os.Application.GlobalCatalog;

public sealed record SupplierItemSearchPage(
    string? Query,
    string? SupplierCode,
    string? VehicleType,
    int? Year,
    string? Make,
    string? Model,
    string? Category,
    string? Brand,
    string? TireBrand,
    string? TireModelLine,
    int? TireWidth,
    int? TireAspectRatio,
    int? TireRimDiameter,
    string? TirePosition,
    IReadOnlyCollection<SupplierSearchOption> AvailableSuppliers,
    IReadOnlyCollection<SupplierSearchOption> ConfiguredSuppliers,
    IReadOnlyCollection<string> AvailableCategories,
    IReadOnlyCollection<string> AvailableBrands,
    IReadOnlyCollection<string> AvailableVehicleTypes,
    IReadOnlyCollection<int> AvailableYears,
    IReadOnlyCollection<string> AvailableMakes,
    IReadOnlyCollection<string> AvailableModels,
    int TotalResults,
    int Offset,
    int PageSize,
    bool HasMore,
    IReadOnlyCollection<SupplierItemSearchResult> Results,
    bool IsSearchExecuted);

public sealed record SupplierItemSearchRequest(
    string? Query,
    string? SupplierCode,
    string? VehicleType,
    int? Year,
    string? Make,
    string? Model,
    int Offset = 0,
    bool SearchExecuted = false,
    bool IncludeFacets = true,
    string? Category = null,
    string? Brand = null,
    string? TireBrand = null,
    string? TireModelLine = null,
    int? TireWidth = null,
    int? TireAspectRatio = null,
    int? TireRimDiameter = null,
    string? TirePosition = null);

public sealed record SupplierSearchOption(
    string Code,
    string Name,
    bool IsConfigured,
    bool IsEnabled);

public sealed record SupplierItemSearchResult(
    Guid SupplierProductId,
    Guid GlobalProductId,
    string SupplierCode,
    string SupplierName,
    string SupplierSku,
    string? ManufacturerPartNumber,
    string? Upc,
    string Brand,
    string Title,
    string? Category,
    string? LongDescription,
    string? ProductFeatures,
    string Status,
    int FitmentRecordCount,
    decimal? Msrp,
    decimal? DealerCost,
    decimal? ActualCost,
    string? ImageUrl,
    IReadOnlyCollection<SupplierItemCrossReferenceResult> CrossReferences,
    bool IsCrossReference = false,
    IReadOnlyCollection<SupplierItemOfferResult>? Offers = null,
    IReadOnlyCollection<SupplierItemFitmentResult>? Fitment = null);

public sealed record SupplierItemOfferResult(
    Guid SupplierProductId,
    Guid GlobalProductId,
    string SupplierCode,
    string SupplierName,
    string SupplierSku,
    string? ManufacturerPartNumber,
    string? Upc,
    string Brand,
    string Title,
    string? Category,
    string? LongDescription,
    string? ProductFeatures,
    string Status,
    int FitmentRecordCount,
    decimal? Msrp,
    decimal? DealerCost,
    decimal? ActualCost,
    string? ImageUrl,
    bool HasCachedInventory,
    int? CachedInventoryTotal,
    bool IsPreferredSupplier,
    string? PreferredWarehouseCode,
    string? PreferredWarehouseName,
    bool IsDefaultOffer);

public sealed record SupplierItemCrossReferenceResult(
    Guid SupplierProductId,
    Guid GlobalProductId,
    string SupplierCode,
    string SupplierName,
    string SupplierSku,
    string? ManufacturerPartNumber,
    string Brand,
    string Title,
    string Status);

public sealed record SupplierItemFitmentResult(
    int Year,
    string Make,
    string Model,
    string? Submodel,
    string? Engine,
    string? Notes,
    string? VehicleType,
    IReadOnlyCollection<string> SupplierCodes,
    IReadOnlyCollection<string> SupplierSkus);

public interface ISupplierItemSearchService
{
    Task<SupplierItemSearchPage> SearchAsync(string? query, int limit, CancellationToken cancellationToken);

    Task<SupplierItemSearchPage> SearchAsync(SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken);

    Task<SupplierItemSearchPage> SearchForCompanyAsync(Guid organizationId, SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken);

    Task<int> CountFitmentItemsForCompanyAsync(Guid organizationId, string? vehicleType, int year, string make, string model, CancellationToken cancellationToken);
}
