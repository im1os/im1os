namespace iM1os.Application.Payments;

public sealed record PaymentsWorkspace(
    PaymentsConfigurationStatus Configuration,
    IReadOnlyCollection<PaymentTransactionRow> Transactions,
    decimal ApprovedSalesTotal,
    decimal RefundedTotal,
    int ApprovedCount,
    int DeclinedCount,
    int PendingCount);

public sealed record PaymentsConfigurationStatus(
    bool IsConfigured,
    string Provider,
    string Environment,
    string? PublicTokenizationKey,
    string CollectJsUrl,
    string PaymentsBaseUrl,
    bool HasActiveMerchant,
    bool HasMerchantPrivateKey,
    bool HasPartnerApiKey);

public sealed record PaymentTransactionRow(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string TransactionType,
    string PaymentMethod,
    decimal Amount,
    string Currency,
    string Status,
    bool IsApproved,
    string? GatewayTransactionId,
    string? AuthorizationCode,
    string? ResponseCode,
    string? ResponseText,
    string? OrderId,
    string? Description,
    string? CustomerName,
    string? CustomerEmail,
    string? CardBrand,
    string? CardLastFour);

public sealed record PaymentSaleRequest(
    string PaymentToken,
    decimal Amount,
    string? Currency = null,
    Guid? LocationId = null,
    string? OrderId = null,
    string? ReferenceType = null,
    string? ReferenceId = null,
    string? Description = null,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? Phone = null,
    string? AddressLine1 = null,
    string? City = null,
    string? Region = null,
    string? PostalCode = null,
    string? Country = null,
    string? CardBrand = null,
    string? CardLastFour = null);

public sealed record PaymentTransactionResult(
    bool Success,
    Guid PaymentTransactionId,
    string? GatewayTransactionId,
    string? AuthorizationCode,
    string? ResponseCode,
    string? ResponseText,
    string Status);
