using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Merchant;

public sealed class MerchantProviderRelationship : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid MerchantAccountId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderMerchantId { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? CapabilitiesJson { get; set; }

    public string? ProviderReference { get; set; }

    public string? LastProviderError { get; set; }

    public string? SupportNotes { get; set; }

    public string? GatewayUsername { get; set; }

    public string? GatewayPasswordProtected { get; set; }

    public string? PaymentApiKeyProtected { get; set; }

    public string? QueryApiKeyProtected { get; set; }

    public string? PublicTokenizationKey { get; set; }

    public string? CredentialMetadataJson { get; set; }
}
