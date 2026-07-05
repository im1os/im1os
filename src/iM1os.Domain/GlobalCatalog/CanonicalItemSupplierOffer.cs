using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CanonicalItemSupplierOffer : AuditableEntity
{
    public Guid CanonicalItemId { get; set; }

    public Guid SupplierId { get; set; }

    public Guid SupplierProductId { get; set; }

    public required string SupplierCode { get; set; }

    public required string SupplierSku { get; set; }

    public string? SupplierPartNumber { get; set; }

    public string? SupplierTitle { get; set; }

    public decimal? ListPrice { get; set; }

    public decimal? DealerCost { get; set; }

    public string? WarehouseAvailability { get; set; }

    public string? ImageUrl { get; set; }

    public required string Status { get; set; }
}
