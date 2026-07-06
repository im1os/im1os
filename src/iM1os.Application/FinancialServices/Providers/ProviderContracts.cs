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
    string? ExternalIdentifier);

public sealed record PartnerMerchantCreateResult(
    string ProviderCode,
    string ProviderMerchantId,
    string? GatewayUsername,
    string? GatewayPassword,
    string Status,
    string? RawResponse,
    string? ProviderReference = null);

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

    Task<PartnerMerchantCreateResult> CreateMerchantAsync(PartnerMerchantCreateRequest request, CancellationToken cancellationToken);

    Task<PartnerMerchantCreateResult> SubmitMerchantApplicationAsync(
        string providerReference,
        PartnerMerchantCreateRequest request,
        CancellationToken cancellationToken);

    Task<PartnerMerchantCreateResult> GetMerchantApplicationStatusAsync(
        string providerReference,
        CancellationToken cancellationToken);

    Task<PartnerMerchantCredentialResult> CreateMerchantCredentialAsync(
        PartnerMerchantCredentialRequest request,
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
