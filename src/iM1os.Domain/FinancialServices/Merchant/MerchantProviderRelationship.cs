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

    public string? ApplicationCreateIdempotencyKey { get; set; }

    public string? ApplicationSubmitIdempotencyKey { get; set; }

    public string? PaymentCredentialIdempotencyKey { get; set; }

    public string? TokenizationCredentialIdempotencyKey { get; set; }

    public DateTimeOffset? ProviderApplicationCreatedAtUtc { get; set; }

    public DateTimeOffset? LegalConsentCompletedAtUtc { get; set; }

    public DateTimeOffset? ProviderApplicationSubmittedAtUtc { get; set; }

    public DateTimeOffset? ProviderStatusRefreshedAtUtc { get; set; }

    public DateTimeOffset? ProviderApprovedAtUtc { get; set; }

    public DateTimeOffset? CredentialsProvisionedAtUtc { get; set; }

    public string? LegalConsentUrlProtected { get; set; }

    public string CredentialProvisioningStatus { get; set; } = "NotStarted";

    public string? LastProviderError { get; set; }

    public string? SupportNotes { get; set; }

    public string? GatewayUsername { get; set; }

    public string? GatewayPasswordProtected { get; set; }

    public string? PaymentApiKeyProtected { get; set; }

    public string? QueryApiKeyProtected { get; set; }

    public string? PublicTokenizationKeyProtected { get; set; }

    public string? CredentialMetadataJson { get; set; }
}
