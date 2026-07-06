using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Wallet;

public sealed class CustomerWallet : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public string Status { get; set; } = "Active";

    public Guid? PreferredPaymentMethodId { get; set; }
}
