using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Billing;

public sealed class SubscriptionAgreement : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid? CustomerId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string BillingCadence { get; set; } = "Monthly";

    public string Status { get; set; } = "Draft";
}
