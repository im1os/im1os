using System.Net;
using iM1os.Application.Payments;
using iM1os.Infrastructure.FinancialServices.Providers;
using iM1os.Infrastructure.Configuration;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using iM1os.Domain.FinancialServices.Merchant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace iM1os.Tests;

public sealed class Im1PaymentsServiceTests
{
    private static readonly TestSecretProtector SecretProtector = new();

    [Fact]
    public async Task CreateSaleAsync_sends_the_exact_json_media_type_required_by_nmi()
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("""
            {
              "id": "28147497671995",
              "auth_code": "TAS536",
              "response": "1",
              "response_text": "Approved",
              "status": "approved"
            }
            """);
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddActiveMerchant(dbContext, organizationId);

        await service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest("tok_exact_json", 1m, "USD"),
            CancellationToken.None);

        Assert.Equal("application/json", handler.RequestContentType);
        Assert.Null(handler.RequestContentTypeCharSet);
        Assert.Contains("application/json", handler.AcceptMediaTypes);
    }

    [Fact]
    public async Task CreateSaleAsync_uses_the_nmi_v5_billing_schema_and_omits_unconfigured_merchant_fields()
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("""
            {
              "id": "28147497671995",
              "response": "1",
              "response_text": "Approved",
              "status": "approved"
            }
            """);
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddActiveMerchant(dbContext, organizationId);

        await service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest(
                "tok_v5_schema",
                1m,
                "USD",
                OrderId: "TEST-1001",
                PostalCode: "60601"),
            CancellationToken.None);

        Assert.Contains("\"zip\":\"60601\"", handler.RequestBody);
        Assert.DoesNotContain("postal_code", handler.RequestBody);
        Assert.DoesNotContain("merchant_defined_fields", handler.RequestBody);
        Assert.DoesNotContain("order_details", handler.RequestBody);
        Assert.DoesNotContain("im1_", handler.RequestBody);

        var transaction = await dbContext.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("TEST-1001", transaction.OrderId);
    }

    [Fact]
    public async Task CreateSaleAsync_records_approved_transaction_ledger_entry_and_domain_event()
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("""
            {
              "object": "transaction",
              "id": "28147497671995",
              "type": "sale",
              "amount": "10.00",
              "auth_code": "TAS536",
              "response": "1",
              "response_text": "Approved",
              "status": "approved"
            }
            """);
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddActiveMerchant(dbContext, organizationId);

        var result = await service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest(
                "tok_123",
                10m,
                "USD",
                OrderId: "WO-1001",
                Description: "Deposit",
                FirstName: "Ada",
                LastName: "Rider",
                Email: "ada@example.test",
                CardBrand: "visa",
                CardLastFour: "1111"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("28147497671995", result.GatewayTransactionId);
        Assert.Equal("TAS536", result.AuthorizationCode);
        Assert.Equal("merchant-private-key", handler.AuthorizationHeader);
        Assert.Contains("\"payment_token\":\"tok_123\"", handler.RequestBody!);

        var transaction = await dbContext.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(organizationId, transaction.OrganizationId);
        Assert.Equal("Approved", transaction.Status);
        Assert.Equal(10m, transaction.Amount);
        Assert.Equal("Ada Rider", transaction.CustomerName);
        Assert.Equal("1111", transaction.CardLastFour);
        Assert.Equal("NMI", transaction.Provider);
        Assert.Null(transaction.RawResponseJson);

        var ledgerEntry = await dbContext.FinancialLedgerEntries.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(transaction.Id.ToString(), ledgerEntry.SourceId);
        Assert.Equal("PaymentApproved", ledgerEntry.EntryType);
        Assert.Equal(10m, ledgerEntry.Amount);
        Assert.Equal("NMI", ledgerEntry.Provider);

        var domainEvent = await dbContext.DomainEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("PaymentApproved", domainEvent.EventType);
        Assert.Equal("FinancialServices", domainEvent.SourceModule);
    }

    [Fact]
    public async Task GetWorkspaceAsync_reports_configuration_and_totals()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        dbContext.PaymentTransactions.Add(new()
        {
            OrganizationId = organizationId,
            Amount = 25m,
            IsApproved = true,
            Status = "Approved",
            TransactionType = "Sale"
        });
        dbContext.PaymentTransactions.Add(new()
        {
            OrganizationId = organizationId,
            Amount = 5m,
            IsApproved = false,
            Status = "Declined",
            TransactionType = "Sale"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new RecordingHandler("{}"));
        AddActiveMerchant(dbContext, organizationId);

        var workspace = await service.GetWorkspaceAsync(organizationId, CancellationToken.None);

        Assert.True(workspace.Configuration.IsConfigured);
        Assert.True(workspace.Configuration.HasActiveMerchant);
        Assert.Equal("Sandbox", workspace.Configuration.Environment);
        Assert.Equal(25m, workspace.ApprovedSalesTotal);
        Assert.Equal(1, workspace.ApprovedCount);
        Assert.Equal(1, workspace.DeclinedCount);
    }

    [Fact]
    public async Task CreateSaleAsync_records_safe_nmi_http_error_message_without_raw_response()
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("""
            {
              "type": "authenticationError",
              "error_code": "E_AUTHENTICATION_MISSING",
              "message": "Missing/Invalid Authentication",
              "ref_id": "83485292"
            }
            """, HttpStatusCode.Unauthorized);
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddActiveMerchant(dbContext, organizationId);

        var result = await service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest("tok_123", 10m),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Error", result.Status);
        Assert.Equal("401", result.ResponseCode);
        Assert.Equal("Missing/Invalid Authentication", result.ResponseText);

        var transaction = await dbContext.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Null(transaction.RawResponseJson);
        Assert.Equal("Missing/Invalid Authentication", transaction.ResponseText);
    }

    [Fact]
    public async Task CreateSaleAsync_records_only_allowlisted_nmi_payment_validation_details()
    {
        await using var dbContext = CreateContext();
        const string submittedToken = "tok_sensitive_value_must_not_persist";
        var handler = new RecordingHandler($$"""
            {
              "type": "validationError",
              "error_code": "E_VALIDATION",
              "message": "The provided data is invalid.",
              "details": [
                {
                  "fieldName": "payment_details.payment_token",
                  "message": "The payment token {{submittedToken}} is invalid."
                },
                {
                  "fieldName": "billing_address.zip",
                  "message": "ZIP format is invalid."
                },
                {
                  "fieldName": "unsupported.private_value",
                  "message": "Unsafe value 123456789 should not persist."
                }
              ]
            }
            """, HttpStatusCode.UnprocessableEntity);
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddActiveMerchant(dbContext, organizationId);

        var result = await service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest(
                submittedToken,
                1m,
                "USD",
                Email: "private@example.test",
                PostalCode: "60601"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("422", result.ResponseCode);
        Assert.Contains("billing_address.zip", result.ResponseText);
        Assert.Contains("payment_details.payment_token", result.ResponseText);
        Assert.Contains("E_VALIDATION", result.ResponseText);
        Assert.Contains("ZIP format is invalid.", result.ResponseText);
        Assert.DoesNotContain(submittedToken, result.ResponseText);
        Assert.DoesNotContain("private@example.test", result.ResponseText);
        Assert.DoesNotContain("unsupported.private_value", result.ResponseText);
        Assert.DoesNotContain("123456789", result.ResponseText);

        var transaction = await dbContext.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Null(transaction.RawResponseJson);
        Assert.Equal(result.ResponseText, transaction.ResponseText);
    }

    [Fact]
    public async Task CreateSaleAsync_persists_declined_payment_attempt_and_history()
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("""
            {
              "object": "transaction",
              "id": "declined-transaction-123",
              "type": "sale",
              "amount": "10.00",
              "response": "2",
              "response_text": "Do not honor",
              "status": "declined"
            }
            """);
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddActiveMerchant(dbContext, organizationId);

        var result = await service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest("tok_declined", 0.50m, CardBrand: "visa", CardLastFour: "4242"),
            CancellationToken.None);
        var workspace = await service.GetWorkspaceAsync(organizationId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Declined", result.Status);
        Assert.Equal("Do not honor", result.ResponseText);
        var transaction = await dbContext.PaymentTransactions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Declined", transaction.Status);
        Assert.False(transaction.IsApproved);
        Assert.Equal(0.50m, transaction.Amount);
        Assert.Equal("declined-transaction-123", transaction.GatewayTransactionId);
        Assert.Null(transaction.RawResponseJson);
        Assert.Single(workspace.Transactions);
        Assert.Equal(1, workspace.DeclinedCount);

        var ledgerEntry = await dbContext.FinancialLedgerEntries.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("PaymentDeclined", ledgerEntry.EntryType);
        var domainEvent = await dbContext.DomainEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("PaymentDeclined", domainEvent.EventType);
    }

    [Fact]
    public async Task CreateSaleAsync_requires_active_merchant_account()
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("{}");
        var service = CreateService(dbContext, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSaleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PaymentSaleRequest("tok_123", 10m),
            CancellationToken.None));

        Assert.Equal("An active merchant account is required before payments can run.", ex.Message);
        Assert.Null(handler.RequestBody);
        Assert.Empty(await dbContext.PaymentTransactions.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task GetWorkspaceAsync_does_not_expose_fallback_tokenization_key_without_active_merchant()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext, new RecordingHandler("{}"));

        var workspace = await service.GetWorkspaceAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(workspace.Configuration.IsConfigured);
        Assert.False(workspace.Configuration.HasActiveMerchant);
        Assert.Null(workspace.Configuration.PublicTokenizationKey);
        Assert.False(workspace.Configuration.HasMerchantPrivateKey);
    }

    [Theory]
    [InlineData(MerchantAccountStatuses.Rejected)]
    [InlineData(MerchantAccountStatuses.Suspended)]
    public async Task CreateSaleAsync_blocks_rejected_and_suspended_merchants(string merchantStatus)
    {
        await using var dbContext = CreateContext();
        var handler = new RecordingHandler("{}");
        var service = CreateService(dbContext, handler);
        var organizationId = Guid.NewGuid();
        AddMerchant(dbContext, organizationId, merchantStatus);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSaleAsync(
            organizationId,
            Guid.NewGuid(),
            new PaymentSaleRequest("tok_123", 10m),
            CancellationToken.None));

        Assert.Equal("An active merchant account is required before payments can run.", ex.Message);
        Assert.Null(handler.RequestBody);
        Assert.Empty(await dbContext.PaymentTransactions.IgnoreQueryFilters().ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new SystemClock(), new TenantProvider(currentUser));
    }

    private static PaymentService CreateService(ApplicationDbContext dbContext, RecordingHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sandbox.nmi.com/api/v5/")
        };
        var currentUser = new NoCurrentUser();
        return new PaymentService(
            dbContext,
            new NmiPaymentProvider(
                new StaticHttpClientFactory(client),
                Options.Create(new NmiPaymentOptions
                {
                    Environment = "Sandbox",
                    PaymentsBaseUrl = "https://sandbox.nmi.com/api/v5/",
                    MerchantPrivateKey = "test-private-key",
                    MerchantTokenizationKey = "test-public-key"
                })),
            new DomainEventRecorder(dbContext, currentUser, new SystemClock()),
            new SystemClock(),
            SecretProtector);
    }

    private static void AddActiveMerchant(ApplicationDbContext dbContext, Guid organizationId)
    {
        AddMerchant(dbContext, organizationId, MerchantAccountStatuses.Active);
    }

    private static void AddMerchant(ApplicationDbContext dbContext, Guid organizationId, string status)
    {
        var merchantAccount = new MerchantAccount
        {
            OrganizationId = organizationId,
            Status = status,
            UnderwritingStatus = status == MerchantAccountStatuses.Active ? "Approved" : status,
            LegalBusinessName = "Test Merchant",
            PrimaryProviderCode = "NMI",
            PaymentsEnabled = status == MerchantAccountStatuses.Active
        };
        dbContext.MerchantAccounts.Add(merchantAccount);
        dbContext.SaveChanges();
        dbContext.MerchantProviderRelationships.Add(new MerchantProviderRelationship
        {
            OrganizationId = organizationId,
            MerchantAccountId = merchantAccount.Id,
            ProviderCode = "NMI",
            ProviderMerchantId = "nmi-merchant-123",
            Status = status,
            CredentialProvisioningStatus = status == MerchantAccountStatuses.Active ? "Complete" : "NotStarted",
            PaymentApiKeyProtected = status == MerchantAccountStatuses.Active
                ? SecretProtector.Protect("merchant-private-key")
                : null,
            PublicTokenizationKeyProtected = status == MerchantAccountStatuses.Active
                ? SecretProtector.Protect("merchant-public-key")
                : null
        });
        dbContext.SaveChanges();
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? AuthorizationHeader { get; private set; }

        public string? RequestBody { get; private set; }

        public string? RequestContentType { get; private set; }

        public string? RequestContentTypeCharSet { get; private set; }

        public IReadOnlyList<string> AcceptMediaTypes { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationHeader = request.Headers.TryGetValues("Authorization", out var values)
                ? values.SingleOrDefault()
                : null;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestContentType = request.Content?.Headers.ContentType?.MediaType;
            RequestContentTypeCharSet = request.Content?.Headers.ContentType?.CharSet;
            AcceptMediaTypes = request.Headers.Accept
                .Select(x => x.MediaType)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
