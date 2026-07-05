using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class CompanyInventoryMovement : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid CompanyInventoryItemId { get; set; }

    public Guid? LocationId { get; set; }

    public string MovementType { get; set; } = "Adjustment";

    public decimal QuantityDelta { get; set; }

    public decimal QuantityAfter { get; set; }

    public decimal? UnitCost { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }
}
