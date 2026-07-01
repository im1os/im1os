namespace iM1os.Application.GlobalCatalog;

public sealed record WpsLiveInventoryResult(
    bool IsAvailable,
    string? Message,
    IReadOnlyCollection<WpsWarehouseInventoryRow> Warehouses);

public sealed record WpsWarehouseInventoryRow(
    string WarehouseCode,
    string WarehouseName,
    int? Quantity,
    string QuantityDisplay);

public interface IWpsLiveInventoryService
{
    Task<WpsLiveInventoryResult> GetInventoryAsync(Guid supplierProductId, CancellationToken cancellationToken);

    Task<WpsLiveInventoryResult> GetInventoryForCompanyAsync(Guid organizationId, Guid supplierProductId, CancellationToken cancellationToken);
}

public sealed record Turn14LiveInventoryResult(
    bool IsAvailable,
    string? Message,
    IReadOnlyCollection<Turn14WarehouseInventoryRow> Warehouses);

public sealed record Turn14WarehouseInventoryRow(
    string WarehouseCode,
    string WarehouseName,
    int? Quantity,
    string QuantityDisplay);

public interface ITurn14LiveInventoryService
{
    Task<Turn14LiveInventoryResult> GetInventoryAsync(Guid supplierProductId, CancellationToken cancellationToken);

    Task<Turn14LiveInventoryResult> GetInventoryForCompanyAsync(Guid organizationId, Guid supplierProductId, CancellationToken cancellationToken);
}

public sealed record PartsUnlimitedLiveInventoryBatchResult(
    bool IsAvailable,
    string? Message,
    IReadOnlyCollection<PartsUnlimitedPartInventoryResult> Items);

public sealed record PartsUnlimitedPartInventoryResult(
    Guid SupplierProductId,
    string SupplierSku,
    bool IsAvailable,
    string? Message,
    IReadOnlyCollection<PartsUnlimitedWarehouseInventoryRow> Warehouses);

public sealed record PartsUnlimitedWarehouseInventoryRow(
    string WarehouseCode,
    string WarehouseName,
    int? Quantity,
    string QuantityDisplay);

public interface IPartsUnlimitedLiveInventoryService
{
    Task<PartsUnlimitedLiveInventoryBatchResult> GetInventoryAsync(IReadOnlyCollection<Guid> supplierProductIds, CancellationToken cancellationToken);

    Task<PartsUnlimitedLiveInventoryBatchResult> GetInventoryForCompanyAsync(Guid organizationId, IReadOnlyCollection<Guid> supplierProductIds, CancellationToken cancellationToken);
}
