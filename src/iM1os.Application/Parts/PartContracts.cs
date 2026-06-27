namespace iM1os.Application.Parts;

public sealed record CreateManufacturerPartRequest(
    string ManufacturerPartNumber,
    string? Upc,
    string Brand,
    string Description,
    string? Category,
    string? Subcategory,
    IReadOnlyCollection<string> ImageUrls,
    decimal? Weight,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    decimal? Msrp,
    decimal? Map,
    string Status,
    IReadOnlyCollection<CreatePartCrossReferenceRequest> CrossReferences);

public sealed record CreatePartCrossReferenceRequest(string ReferenceType, string ReferenceValue, string? Brand, string? Notes);

public sealed record AddSupplierListingRequest(
    Guid ManufacturerPartId,
    string Supplier,
    string SupplierSku,
    decimal? SupplierCost,
    decimal? SupplierMsrp,
    int? WarehouseInventory,
    string WarehouseAvailability,
    int? LeadTimeDays,
    string? FreightClass,
    bool IsPromotionEligible,
    DateTimeOffset? LastSyncAtUtc);

public sealed record SetInventoryItemRequest(
    Guid ManufacturerPartId,
    Guid? LocationId,
    string? BinLocation,
    int QuantityOnHand,
    int QuantityAllocated,
    decimal? AverageCost,
    decimal? LastCost,
    int? ReorderPoint);

public sealed record SupersedePartRequest(Guid SupersededByManufacturerPartId);

public sealed record PartSearchResult(
    Guid Id,
    string ManufacturerPartNumber,
    string? Upc,
    string Brand,
    string Description,
    string? Category,
    string? Subcategory,
    string Status);

public sealed record PartDetail(
    Guid Id,
    string ManufacturerPartNumber,
    string? Upc,
    string Brand,
    string Description,
    string? Category,
    string? Subcategory,
    decimal? Weight,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    decimal? Msrp,
    decimal? Map,
    string Status,
    PartSummary? SupersededBy,
    IReadOnlyCollection<string> Images,
    IReadOnlyCollection<PartCrossReferenceDetail> CrossReferences,
    IReadOnlyCollection<SupplierListingDetail> SupplierListings,
    IReadOnlyCollection<InventoryItemDetail> Inventory);

public sealed record PartSummary(Guid Id, string ManufacturerPartNumber, string Brand, string Description);

public sealed record PartCrossReferenceDetail(string ReferenceType, string ReferenceValue, string? Brand, string? Notes);

public sealed record SupplierListingDetail(
    Guid Id,
    string Supplier,
    string SupplierSku,
    decimal? SupplierCost,
    decimal? SupplierMsrp,
    int? WarehouseInventory,
    string WarehouseAvailability,
    int? LeadTimeDays,
    string? FreightClass,
    bool IsPromotionEligible,
    DateTimeOffset LastSyncAtUtc);

public sealed record InventoryItemDetail(
    Guid Id,
    Guid? LocationId,
    string? BinLocation,
    int QuantityOnHand,
    int QuantityAllocated,
    int QuantityAvailable,
    decimal? AverageCost,
    decimal? LastCost,
    int? ReorderPoint);
