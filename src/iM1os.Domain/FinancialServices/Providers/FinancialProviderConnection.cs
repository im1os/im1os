using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Providers;

public sealed class FinancialProviderConnection : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderType { get; set; } = "Payment";

    public string Status { get; set; } = "Active";

    public string? CapabilitiesJson { get; set; }

    public string? ConfigurationReference { get; set; }
}
