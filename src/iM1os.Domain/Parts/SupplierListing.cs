using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class SupplierListing : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid ManufacturerPartId { get; set; }

    public required string Supplier { get; set; }

    public required string SupplierSku { get; set; }

    public decimal? SupplierCost { get; set; }

    public decimal? SupplierMsrp { get; set; }

    public int? WarehouseInventory { get; set; }

    public required string WarehouseAvailability { get; set; }

    public int? LeadTimeDays { get; set; }

    public string? FreightClass { get; set; }

    public bool IsPromotionEligible { get; set; }

    public DateTimeOffset LastSyncAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
