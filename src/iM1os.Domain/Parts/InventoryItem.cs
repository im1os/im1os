using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class InventoryItem : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid ManufacturerPartId { get; set; }

    public string? BinLocation { get; set; }

    public int QuantityOnHand { get; set; }

    public int QuantityAllocated { get; set; }

    public int QuantityAvailable { get; set; }

    public decimal? AverageCost { get; set; }

    public decimal? LastCost { get; set; }

    public int? ReorderPoint { get; set; }
}
