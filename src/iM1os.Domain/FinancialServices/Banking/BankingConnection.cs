using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Banking;

public sealed class BankingConnection : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? AccountDescriptor { get; set; }

    public string? ProviderToken { get; set; }
}
