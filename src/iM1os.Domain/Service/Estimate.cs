using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class Estimate : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid WorkOrderId { get; set; }

    public required string EstimateNumber { get; set; }

    public required string Status { get; set; }

    public string DepositTerms { get; set; } = "No Deposit";

    public string? PaymentTerms { get; set; }

    public decimal FeesTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal Subtotal { get; set; }

    public decimal LaborTotal { get; set; }

    public decimal PartsTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public DateTimeOffset CreatedForCustomerAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset? DeclinedAtUtc { get; set; }

    public ICollection<EstimateLineItem> LineItems { get; } = new List<EstimateLineItem>();
}
