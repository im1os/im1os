using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class EstimateLineItem : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EstimateId { get; set; }

    public Guid WorkOrderId { get; set; }

    public Guid? LaborOperationId { get; set; }

    public Guid? SupplierId { get; set; }

    public Guid? SupplierProductId { get; set; }

    public Guid? ManufacturerPartId { get; set; }

    public Guid? InventoryItemId { get; set; }

    public string LineType { get; set; } = "Labor";

    public required string Description { get; set; }

    public string? Notes { get; set; }

    public string? Sku { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public decimal Rate { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsTaxable { get; set; } = true;

    public bool IsDeclined { get; set; }

    public bool IsDone { get; set; }

    public int SortOrder { get; set; }
}
