using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class SupplierProduct : AuditableEntity
{
    public Guid SupplierId { get; set; }

    public Guid GlobalProductId { get; set; }

    public required string SupplierSku { get; set; }

    public string? SourceSupplierProductId { get; set; }

    public string? SupplierDescription { get; set; }

    public string? SupplierPartNumber { get; set; }

    public string? ManufacturerPartNumber { get; set; }

    public string? NormalizedManufacturerPartNumber { get; set; }

    public required string SupplierStatus { get; set; }

    public string? Packaging { get; set; }

    public int? MinimumOrder { get; set; }

    public int? CaseQuantity { get; set; }

    public string? WarehouseAvailability { get; set; }

    public string? SupplierImagesJson { get; set; }

    public string? SourceDataJson { get; set; }

    public DateTimeOffset? LastSyncedAtUtc { get; set; }
}
