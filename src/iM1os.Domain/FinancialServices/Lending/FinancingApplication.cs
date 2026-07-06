using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Lending;

public sealed class FinancingApplication : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid? CustomerId { get; set; }

    public decimal RequestedAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "Draft";

    public string? ProviderCode { get; set; }
}
