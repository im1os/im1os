using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class CompanyInventoryItem : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid? GlobalProductId { get; set; }

    public Guid? SupplierProductId { get; set; }

    public string SourceType { get; set; } = "Custom";

    public string? SourceSupplierCode { get; set; }

    public string? SourceSupplierName { get; set; }

    public string? SourceSupplierSku { get; set; }

    public string? SourceSupplierProductId { get; set; }

    public string? Sku { get; set; }

    public string? ManufacturerPartNumber { get; set; }

    public string? NormalizedManufacturerPartNumber { get; set; }

    public string? Upc { get; set; }

    public string? Brand { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? Subcategory { get; set; }

    public string? ImageUrl { get; set; }

    public decimal? RetailPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public decimal? DefaultCost { get; set; }

    public decimal? AverageCost { get; set; }

    public decimal? LastCost { get; set; }

    public bool IsStockedInStore { get; set; }

    public bool TrackInventory { get; set; } = true;

    public bool IsSerialized { get; set; }

    public bool IsActive { get; set; } = true;

    public string Status { get; set; } = "Active";

    public string? Notes { get; set; }
}
