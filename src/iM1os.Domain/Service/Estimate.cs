using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class Estimate : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid WorkOrderId { get; set; }

    public required string EstimateNumber { get; set; }

    public required string Status { get; set; }

    public decimal LaborTotal { get; set; }

    public decimal PartsTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public DateTimeOffset CreatedForCustomerAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset? DeclinedAtUtc { get; set; }
}
