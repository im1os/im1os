using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class CompanyInventoryLocationStock : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid CompanyInventoryItemId { get; set; }

    public Guid? LocationId { get; set; }

    public string? LocationNameSnapshot { get; set; }

    public string? BinLocation { get; set; }

    public decimal QuantityOnHand { get; set; }

    public decimal QuantityAllocated { get; set; }

    public decimal QuantityAvailable { get; set; }

    public decimal QuantityOnOrder { get; set; }

    public decimal QuantityBackordered { get; set; }

    public decimal? MinQuantity { get; set; }

    public decimal? MaxQuantity { get; set; }

    public decimal? ReorderPoint { get; set; }

    public decimal? ReorderQuantity { get; set; }

    public bool StockInStore { get; set; }

    public bool AllowNegativeStock { get; set; }

    public DateTimeOffset? LastCountedAtUtc { get; set; }
}
