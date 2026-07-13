namespace iM1os.Application.FinancialServices.Providers;

public sealed record FinancialProviderConfiguration(
    bool IsConfigured,
    string ProviderCode,
    string Environment,
    string? PublicTokenizationKey,
    string? HostedFieldsUrl,
    bool HasPaymentCredentials,
    bool HasPlatformCredentials);

public sealed record ProviderPaymentSaleRequest(
    Guid OrganizationId,
    Guid MerchantAccountId,
    string ProviderMerchantId,
    string? PaymentApiKey,
    Guid PaymentTransactionId,
    string PaymentToken,
    decimal Amount,
    string Currency,
    string? OrderId,
    string? ReferenceType,
    string? ReferenceId,
    string? Description,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country);

public sealed record ProviderPaymentResult(
    bool IsApproved,
    string Status,
    string? ProviderTransactionId,
    string? AuthorizationCode,
    string? ResponseCode,
    string? ResponseText,
    string? CardBrand,
    string? CardLastFour,
    string? RawResponse);

public sealed record PartnerMerchantCreateRequest(
    Guid OrganizationId,
    string LegalBusinessName,
    string Country,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string TimeZone,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? Website,
    string? ExternalIdentifier,
    string? Dba = null,
    string? TaxIdentifier = null,
    string? BusinessType = null,
    string? BusinessDescription = null,
    int? YearsInBusiness = null,
    string? OwnerTitle = null,
    decimal? OwnerOwnershipPercentage = null,
    string? OwnerDateOfBirth = null,
    string? OwnerSsn = null,
    string? BankName = null,
    string? BankRoutingNumber = null,
    string? BankAccountNumber = null,
    decimal? ExpectedMonthlyVolume = null,
    decimal? AverageTicket = null,
    decimal? HighTicket = null,
    decimal? CardPresentPercentage = null,
    decimal? KeyEnteredPercentage = null,
    decimal? EcommercePercentage = null,
    decimal? MotoPercentage = null,
    string? Mcc = null);

public sealed record PartnerMerchantCreateResult(
    string ProviderCode,
    string ProviderMerchantId,
    string? GatewayUsername,
    string? GatewayPassword,
    string Status,
    string? RawResponse,
    string? ProviderReference = null,
    string? LegalConsentUrl = null);

public sealed record PartnerMerchantCredentialRequest(
    string ProviderMerchantId,
    string Description,
    IReadOnlyCollection<string> Permissions);

public sealed record PartnerMerchantCredentialResult(
    string? KeyId,
    string? PrivateKey,
    string? PublicKey,
    string? Description,
    string? RawResponse);

public interface IPaymentProvider
{
    string ProviderCode { get; }

    FinancialProviderConfiguration GetConfiguration();

    Task<ProviderPaymentResult> ProcessSaleAsync(ProviderPaymentSaleRequest request, CancellationToken cancellationToken);
}

public interface IPartnerProvider
{
    string ProviderCode { get; }

    FinancialProviderConfiguration GetConfiguration();

    Task<PartnerMerchantCreateResult> CreateMerchantAsync(
        PartnerMerchantCreateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    string GetMerchantApplicationPayloadFingerprint(PartnerMerchantCreateRequest request);

    Task<bool> HasMatchingMerchantApplicationAsync(
        PartnerMerchantCreateRequest request,
        CancellationToken cancellationToken);

    Task<PartnerMerchantCreateResult> SubmitMerchantApplicationAsync(
        string providerReference,
        PartnerMerchantCreateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<PartnerMerchantCreateResult> GetMerchantApplicationStatusAsync(
        string providerReference,
        CancellationToken cancellationToken);

    Task<PartnerMerchantCredentialResult> CreateMerchantCredentialAsync(
        PartnerMerchantCredentialRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface IMerchantProvider
{
    string ProviderCode { get; }
}

public interface ITerminalProvider
{
    string ProviderCode { get; }
}

public interface ICustomerVaultProvider
{
    string ProviderCode { get; }
}

public interface IACHProvider
{
    string ProviderCode { get; }
}

public interface ISubscriptionProvider
{
    string ProviderCode { get; }
}
