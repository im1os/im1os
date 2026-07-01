namespace iM1os.Application.GlobalCatalog;

public sealed record SupplierItemSearchPage(
    string? Query,
    string? SupplierCode,
    string? VehicleType,
    int? Year,
    string? Make,
    string? Model,
    IReadOnlyCollection<SupplierSearchOption> AvailableSuppliers,
    IReadOnlyCollection<SupplierSearchOption> ConfiguredSuppliers,
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
    bool SearchExecuted = false);

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
    string Status,
    int FitmentRecordCount,
    decimal? Msrp,
    decimal? DealerCost,
    decimal? ActualCost,
    string? ImageUrl);

public interface ISupplierItemSearchService
{
    Task<SupplierItemSearchPage> SearchAsync(string? query, int limit, CancellationToken cancellationToken);

    Task<SupplierItemSearchPage> SearchAsync(SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken);

    Task<SupplierItemSearchPage> SearchForCompanyAsync(Guid organizationId, SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken);

    Task<int> CountFitmentItemsForCompanyAsync(Guid organizationId, string? vehicleType, int year, string make, string model, CancellationToken cancellationToken);
}
