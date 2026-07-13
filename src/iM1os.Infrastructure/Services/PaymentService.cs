using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.FinancialServices.Payments;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Application.Payments;
using iM1os.Domain.FinancialServices.Events;
using iM1os.Domain.FinancialServices.Ledger;
using iM1os.Domain.FinancialServices.Merchant;
using iM1os.Domain.FinancialServices.Payments;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class PaymentService(
    IApplicationDbContext dbContext,
    IPaymentProvider paymentProvider,
    IDomainEventRecorder domainEventRecorder,
    IDateTimeProvider dateTimeProvider,
    ISecretProtector secretProtector) : IPaymentService, IIm1PaymentsService
{
    public async Task<PaymentsWorkspace> GetWorkspaceAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var activeMerchant = await GetActiveMerchantRelationshipAsync(organizationId, cancellationToken);
        var transactions = await dbContext.PaymentTransactions.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var rows = transactions.Select(ToRow).ToArray();
        return new PaymentsWorkspace(
            BuildConfigurationStatus(activeMerchant),
            rows,
            transactions.Where(x => x.IsApproved && x.TransactionType == "Sale").Sum(x => x.Amount),
            transactions.Where(x => x.IsApproved && x.TransactionType is "Refund" or "Credit").Sum(x => x.Amount),
            transactions.Count(x => x.IsApproved),
            transactions.Count(x => string.Equals(x.Status, "Declined", StringComparison.OrdinalIgnoreCase)),
            transactions.Count(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<PaymentTransactionResult> CreateSaleAsync(Guid organizationId, Guid actorUserId, PaymentSaleRequest request, CancellationToken cancellationToken)
    {
        var configuration = paymentProvider.GetConfiguration();
        var activeMerchant = await GetActiveMerchantRelationshipAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("An active merchant account is required before payments can run.");
        if (!configuration.HasPaymentCredentials && string.IsNullOrWhiteSpace(activeMerchant.PaymentApiKeyProtected))
        {
            throw new InvalidOperationException("Payment processing credentials are not configured.");
        }

        var paymentToken = Required(request.PaymentToken, "Payment token");
        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        var currency = Clean(request.Currency)?.ToUpperInvariant() ?? "USD";
        var customerName = string.Join(" ", new[] { Clean(request.FirstName), Clean(request.LastName) }.Where(x => x is not null));
        var transaction = new PaymentTransaction
        {
            OrganizationId = organizationId,
            LocationId = request.LocationId,
            Provider = paymentProvider.ProviderCode,
            Environment = configuration.Environment,
            TransactionType = "Sale",
            PaymentMethod = "Card",
            Amount = decimal.Round(request.Amount, 2),
            Currency = currency,
            Status = "Pending",
            OrderId = Clean(request.OrderId),
            ReferenceType = Clean(request.ReferenceType),
            ReferenceId = Clean(request.ReferenceId),
            Description = Clean(request.Description),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName,
            CustomerEmail = Clean(request.Email),
            CustomerPhone = Clean(request.Phone),
            CardBrand = Clean(request.CardBrand),
            CardLastFour = LastFour(request.CardLastFour),
            RequestCorrelationId = Guid.NewGuid().ToString("N")
        };

        dbContext.PaymentTransactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var response = await paymentProvider.ProcessSaleAsync(
                new ProviderPaymentSaleRequest(
                    organizationId,
                    activeMerchant.MerchantAccountId,
                    activeMerchant.ProviderMerchantId,
                    secretProtector.Unprotect(activeMerchant.PaymentApiKeyProtected),
                    transaction.Id,
                    paymentToken,
                    transaction.Amount,
                    transaction.Currency,
                    transaction.OrderId,
                    transaction.ReferenceType,
                    transaction.ReferenceId,
                    transaction.Description,
                    request.FirstName,
                    request.LastName,
                    transaction.CustomerEmail,
                    transaction.CustomerPhone,
                    request.AddressLine1,
                    request.City,
                    request.Region,
                    request.PostalCode,
                    request.Country),
                cancellationToken);
            ApplyProviderResponse(transaction, response);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            transaction.Status = "Error";
            transaction.ResponseText = "Payment request failed before a processor response was recorded.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordLedgerAndEventAsync(transaction, cancellationToken);

        return new PaymentTransactionResult(
            transaction.IsApproved,
            transaction.Id,
            transaction.GatewayTransactionId,
            transaction.AuthorizationCode,
            transaction.ResponseCode,
            transaction.ResponseText,
            transaction.Status);
    }

    private static void ApplyProviderResponse(PaymentTransaction transaction, ProviderPaymentResult response)
    {
        transaction.ResponseCode = response.ResponseCode;
        transaction.ResponseText = SafeResponseText(response.ResponseText);
        transaction.GatewayTransactionId = response.ProviderTransactionId;
        transaction.AuthorizationCode = response.AuthorizationCode;
        transaction.IsApproved = response.IsApproved;
        transaction.Status = response.Status;
        transaction.CardBrand ??= response.CardBrand;
        transaction.CardLastFour ??= response.CardLastFour;
        transaction.RawResponseJson = null;
    }

    private PaymentsConfigurationStatus BuildConfigurationStatus(ActiveMerchantRelationship? activeMerchant)
    {
        var configuration = paymentProvider.GetConfiguration();
        var hasActiveMerchant = activeMerchant is not null;
        var publicKey = !string.IsNullOrWhiteSpace(activeMerchant?.PublicTokenizationKeyProtected)
            ? secretProtector.Unprotect(activeMerchant.PublicTokenizationKeyProtected)
            : null;
        var hasPaymentCredentials = !string.IsNullOrWhiteSpace(activeMerchant?.PaymentApiKeyProtected);
        return new PaymentsConfigurationStatus(
            hasActiveMerchant && hasPaymentCredentials && !string.IsNullOrWhiteSpace(publicKey),
            configuration.ProviderCode,
            configuration.Environment,
            publicKey,
            configuration.HostedFieldsUrl ?? string.Empty,
            string.Empty,
            hasActiveMerchant,
            hasPaymentCredentials,
            configuration.HasPlatformCredentials);
    }

    private async Task<ActiveMerchantRelationship?> GetActiveMerchantRelationshipAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var providerCode = paymentProvider.ProviderCode;
        var activeRelationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.ProviderCode == providerCode &&
                x.Status == MerchantAccountStatuses.Active &&
                x.CredentialProvisioningStatus == "Complete" &&
                x.PaymentApiKeyProtected != null &&
                x.PublicTokenizationKeyProtected != null)
            .Join(
                dbContext.MerchantAccounts.IgnoreQueryFilters().Where(x =>
                    x.OrganizationId == organizationId &&
                    x.Status == MerchantAccountStatuses.Active &&
                    x.PaymentsEnabled),
                relationship => relationship.MerchantAccountId,
                merchant => merchant.Id,
                (relationship, merchant) => new ActiveMerchantRelationship(
                    merchant.Id,
                    relationship.ProviderMerchantId,
                    relationship.PaymentApiKeyProtected!,
                    relationship.PublicTokenizationKeyProtected!))
            .SingleOrDefaultAsync(cancellationToken);

        return activeRelationship;
    }

    private async Task RecordLedgerAndEventAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
    {
        var eventType = transaction.IsApproved
            ? FinancialEventTypes.PaymentApproved
            : FinancialEventTypes.PaymentDeclined;
        var correlationId = transaction.RequestCorrelationId ?? Guid.NewGuid().ToString("N");
        dbContext.FinancialLedgerEntries.Add(new FinancialLedgerEntry
        {
            OrganizationId = transaction.OrganizationId,
            LocationId = transaction.LocationId,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            EntryType = eventType,
            Direction = "In",
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            SourceType = nameof(PaymentTransaction),
            SourceId = transaction.Id.ToString(),
            ReferenceType = transaction.ReferenceType,
            ReferenceId = transaction.ReferenceId,
            Provider = transaction.Provider,
            ProviderTransactionId = transaction.GatewayTransactionId,
            Description = transaction.Description,
            CorrelationId = correlationId
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            transaction.Id,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.Provider,
            transaction.GatewayTransactionId,
            transaction.ReferenceType,
            transaction.ReferenceId
        });
        await domainEventRecorder.RecordAsync(
            new DomainEventRecordRequest(
                transaction.OrganizationId,
                transaction.LocationId,
                nameof(PaymentTransaction),
                transaction.Id.ToString(),
                eventType,
                payload,
                correlationId,
                "FinancialServices"),
            cancellationToken);
    }

    private static PaymentTransactionRow ToRow(PaymentTransaction transaction)
    {
        return new PaymentTransactionRow(
            transaction.Id,
            transaction.CreatedAtUtc,
            transaction.TransactionType,
            transaction.PaymentMethod,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.IsApproved,
            transaction.GatewayTransactionId,
            transaction.AuthorizationCode,
            transaction.ResponseCode,
            transaction.ResponseText,
            transaction.OrderId,
            transaction.Description,
            transaction.CustomerName,
            transaction.CustomerEmail,
            transaction.CardBrand,
            transaction.CardLastFour);
    }

    private static string Required(string? value, string label)
    {
        return Clean(value) ?? throw new InvalidOperationException($"{label} is required.");
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? LastFour(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }

    private static string? SafeResponseText(string? value)
    {
        var clean = Clean(value);
        return clean is null ? null : clean[..Math.Min(clean.Length, 500)];
    }

    private sealed record ActiveMerchantRelationship(
        Guid MerchantAccountId,
        string ProviderMerchantId,
        string PaymentApiKeyProtected,
        string PublicTokenizationKeyProtected);
}
