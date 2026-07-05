namespace iM1os.Application.Inventory;

public sealed record CompanyInventorySearchRequest(
    string? Query = null,
    Guid? LocationId = null,
    bool LowStockOnly = false,
    bool StockedOnly = false);

public sealed record CompanyInventoryWorkspace(
    IReadOnlyCollection<CompanyInventoryItemRow> Items,
    IReadOnlyCollection<CompanyInventoryLocationOption> Locations,
    string? Query,
    Guid? LocationId,
    bool LowStockOnly,
    bool StockedOnly,
    int TotalItems,
    int LowStockItems,
    decimal TotalOnHand,
    decimal TotalAvailable,
    decimal TotalInventoryValue);

public sealed record CompanyInventoryItemRow(
    Guid Id,
    string SourceType,
    string? SourceSupplierCode,
    string? ReplacementSupplierCode,
    string? ReplacementSupplierSku,
    string? Sku,
    string? ManufacturerPartNumber,
    string? Upc,
    string? Brand,
    string Title,
    string? Category,
    string Status,
    string? ImageUrl,
    decimal? RetailPrice,
    decimal? SalePrice,
    decimal? AverageCost,
    decimal TotalOnHand,
    decimal TotalAvailable,
    decimal TotalAllocated,
    decimal TotalOnOrder,
    bool IsStockedInStore,
    bool TrackInventory,
    bool IsLowStock,
    IReadOnlyCollection<CompanyInventoryLocationStockRow> LocationStocks,
    IReadOnlyCollection<CompanyInventoryMovementRow> RecentMovements);

public sealed record CompanyInventoryLocationStockRow(
    Guid Id,
    Guid? LocationId,
    string LocationName,
    string? BinLocation,
    decimal QuantityOnHand,
    decimal QuantityAllocated,
    decimal QuantityAvailable,
    decimal QuantityOnOrder,
    decimal QuantityBackordered,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint,
    decimal? ReorderQuantity,
    bool StockInStore,
    bool AllowNegativeStock,
    bool IsLowStock);

public sealed record CompanyInventoryMovementRow(
    DateTimeOffset CreatedAtUtc,
    string MovementType,
    string LocationName,
    decimal QuantityDelta,
    decimal QuantityAfter,
    string? Reason,
    string? Notes);

public sealed record CompanyInventoryLocationOption(Guid Id, string Name, string Code);

public sealed record CompanyInventoryAddPage(IReadOnlyCollection<CompanyInventoryLocationOption> Locations);

public sealed record CompanyInventorySupplierLookupRequest(
    Guid? SupplierProductId = null,
    string? LookupValue = null,
    string? SupplierCode = null);

public sealed record CompanyInventorySupplierLookupResult(
    bool Found,
    string? Message,
    Guid? SupplierProductId,
    string? SupplierCode,
    string? SupplierName,
    string? SupplierSku,
    string? ManufacturerPartNumber,
    string? Upc,
    string? Brand,
    string? Title,
    string? Category,
    string? ImageUrl,
    decimal? RetailPrice,
    decimal? DealerCost,
    bool AlreadyInInventory,
    decimal CurrentOnHand,
    decimal CurrentAvailable,
    IReadOnlyCollection<CompanyInventoryLocationStockRow> LocationStocks);

public sealed record CompanyInventoryItemRequest(
    string? Sku,
    string? ManufacturerPartNumber,
    string? Upc,
    string? Brand,
    string Title,
    string? Description,
    string? Category,
    string? Subcategory,
    string? ImageUrl,
    decimal? RetailPrice,
    decimal? SalePrice,
    decimal? DefaultCost,
    bool IsStockedInStore,
    bool TrackInventory,
    bool IsSerialized,
    string? Notes,
    Guid? InitialLocationId,
    string? InitialBinLocation,
    decimal InitialQuantityOnHand,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint,
    decimal? ReorderQuantity);

public sealed record CompanyInventorySupplierItemRequest(
    Guid? SupplierProductId,
    string? LookupValue,
    string? SupplierCode,
    Guid? InitialLocationId,
    string? InitialBinLocation,
    decimal InitialQuantityOnHand,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint,
    decimal? ReorderQuantity,
    bool StockInStore,
    string? AssignUpc = null,
    decimal? SalePrice = null);

public sealed record CompanyInventoryLocationStockRequest(
    Guid CompanyInventoryItemId,
    Guid? LocationId,
    string? BinLocation,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint,
    decimal? ReorderQuantity,
    decimal QuantityOnOrder,
    decimal QuantityBackordered,
    bool StockInStore,
    bool AllowNegativeStock);

public sealed record CompanyInventoryStockAdjustmentRequest(
    Guid CompanyInventoryItemId,
    Guid? LocationId,
    decimal QuantityDelta,
    decimal? UnitCost,
    string? Reason,
    string? Notes);

public sealed record CompanyInventoryImportResult(int Processed, int Created, int Updated, int Failed, IReadOnlyCollection<string> Errors);
