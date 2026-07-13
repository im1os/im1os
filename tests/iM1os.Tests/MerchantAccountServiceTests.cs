using iM1os.Application.FinancialServices.Merchant;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Application.Payments;
using iM1os.Domain.FinancialServices.Merchant;
using iM1os.Infrastructure.FinancialServices.Providers;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace iM1os.Tests;

public sealed class MerchantAccountServiceTests
{
    private static readonly TestSecretProtector SecretProtector = new();

    [Fact]
    public async Task Rejection_before_platform_approval_never_calls_NMI()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider();
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        var result = await service.RejectApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            "Underwriting requirements were not met.",
            CancellationToken.None);

        Assert.Equal(MerchantAccountStatuses.Rejected, result.Status);
        Assert.Equal(0, provider.CreateCalls);
        Assert.Equal(0, provider.SubmitCalls);
        Assert.Empty(await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Platform_approval_creates_application_once_and_gates_submission_on_company_consent()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider();
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        var result = await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);
        var workspace = await service.GetCompanyMerchantAccountAsync(organizationId, CancellationToken.None);

        Assert.Equal(MerchantAccountStatuses.LegalConsentRequired, result.Status);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(0, provider.SubmitCalls);
        Assert.True(workspace.LegalConsent.IsRequired);
        Assert.True(workspace.LegalConsent.CanComplete);
        Assert.Equal("https://secure.nmi.com/consent/application-123", workspace.LegalConsent.LegalConsentUrl);
        Assert.Null(workspace.LegalConsent.CompletedAtUtc);
    }

    [Fact]
    public async Task Retrying_approval_reuses_creation_idempotency_key_and_keeps_one_relationship()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider { FailFirstCreate = true };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        var ambiguousFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var result = await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);

        Assert.Equal(MerchantAccountStatuses.LegalConsentRequired, result.Status);
        Assert.Contains("ambiguous", ambiguousFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, provider.CreateCalls);
        Assert.Equal(provider.CreateIdempotencyKeys[0], provider.CreateIdempotencyKeys[1]);
        Assert.Single(provider.CreateIdempotencyKeys.Distinct());
        Assert.Single(await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Safe_NMI_validation_error_is_persisted_without_provider_response_values()
    {
        await using var dbContext = CreateContext();
        var safeException = new NmiValidationException(
            System.Net.HttpStatusCode.UnprocessableEntity,
            ["fld_federal_tax_id"],
            ["invalid_format"],
            ["Value format is invalid."]);
        var provider = new RecordingPartnerProvider { CreateException = safeException };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        await Assert.ThrowsAsync<NmiValidationException>(() => service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();

        Assert.Equal(safeException.Message, relationship.LastProviderError);
        Assert.Contains("HTTP 422", relationship.LastProviderError, StringComparison.Ordinal);
        Assert.Contains("fld_federal_tax_id", relationship.LastProviderError, StringComparison.Ordinal);
        Assert.Contains("invalid_format", relationship.LastProviderError, StringComparison.Ordinal);
        Assert.DoesNotContain("821234567", relationship.LastProviderError, StringComparison.Ordinal);
        Assert.DoesNotContain("111223333", relationship.LastProviderError, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567890", relationship.LastProviderError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Definitive_422_allows_reconciled_versioned_rotation_for_changed_payload()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider
        {
            FirstCreateException = ValidationFailure()
        };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        await Assert.ThrowsAsync<NmiValidationException>(() => service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var relationshipAfterFailure = await dbContext.MerchantProviderRelationships
            .IgnoreQueryFilters()
            .SingleAsync();
        var originalKey = relationshipAfterFailure.ApplicationCreateIdempotencyKey;

        provider.PayloadFingerprint = "PAYLOAD_FINGERPRINT_V2";
        var result = await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
        using var capabilities = JsonDocument.Parse(relationship.CapabilitiesJson!);
        var operation = capabilities.RootElement.GetProperty("ApplicationCreateOperation");
        var rotation = Assert.Single(operation.GetProperty("Rotations").EnumerateArray());

        Assert.Equal(MerchantAccountStatuses.LegalConsentRequired, result.Status);
        Assert.Equal(1, provider.ReconciliationCalls);
        Assert.Equal(2, provider.CreateCalls);
        Assert.NotEqual(originalKey, relationship.ApplicationCreateIdempotencyKey);
        Assert.Contains("nmi-application-create-v2", relationship.ApplicationCreateIdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(2, operation.GetProperty("KeyVersion").GetInt32());
        Assert.Equal("PAYLOAD_FINGERPRINT_V2", operation.GetProperty("PayloadFingerprint").GetString());
        Assert.Equal(2, rotation.GetProperty("KeyVersion").GetInt32());
        Assert.Equal("PAYLOAD_FINGERPRINT_V2", rotation.GetProperty("PayloadFingerprint").GetString());
        Assert.Equal("INVALID_FORMAT", rotation.GetProperty("ReasonCode").GetString());
        Assert.NotEqual(default, rotation.GetProperty("RotatedAtUtc").GetDateTimeOffset());
        Assert.DoesNotContain("821234567", relationship.CapabilitiesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("111223333", relationship.CapabilitiesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567890", relationship.CapabilitiesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_corrected_payload_reuses_existing_key_without_rotation_or_reconciliation()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider
        {
            FirstCreateException = ValidationFailure()
        };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        await Assert.ThrowsAsync<NmiValidationException>(() => service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var result = await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);

        Assert.Equal(MerchantAccountStatuses.LegalConsentRequired, result.Status);
        Assert.Equal(2, provider.CreateCalls);
        Assert.Equal(provider.CreateIdempotencyKeys[0], provider.CreateIdempotencyKeys[1]);
        Assert.Equal(0, provider.ReconciliationCalls);
    }

    [Fact]
    public async Task Rotation_is_blocked_when_provider_reference_exists()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider();
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);
        await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
        var originalKey = relationship.ApplicationCreateIdempotencyKey;

        provider.PayloadFingerprint = "PAYLOAD_FINGERPRINT_V2";
        await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);

        Assert.NotNull(relationship.ProviderReference);
        Assert.Equal(originalKey, relationship.ApplicationCreateIdempotencyKey);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(0, provider.ReconciliationCalls);
    }

    [Fact]
    public async Task Rotation_is_blocked_when_reconciliation_finds_matching_NMI_application()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider
        {
            FirstCreateException = ValidationFailure(),
            MatchingApplicationExists = true
        };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);

        await Assert.ThrowsAsync<NmiValidationException>(() => service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
        var originalKey = relationship.ApplicationCreateIdempotencyKey;
        provider.PayloadFingerprint = "PAYLOAD_FINGERPRINT_V2";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None));

        Assert.Contains("matching application", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.ReconciliationCalls);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(originalKey, relationship.ApplicationCreateIdempotencyKey);
    }

    [Fact]
    public async Task Rotation_is_blocked_after_timeout_5xx_and_ambiguous_response()
    {
        var ambiguousFailures = new Exception[]
        {
            new TaskCanceledException("Simulated timeout."),
            new HttpRequestException(
                "Simulated provider 5xx.",
                null,
                System.Net.HttpStatusCode.ServiceUnavailable),
            new JsonException("Simulated malformed provider response.")
        };

        foreach (var ambiguousFailure in ambiguousFailures)
        {
            await using var dbContext = CreateContext();
            var provider = new RecordingPartnerProvider { FirstCreateException = ambiguousFailure };
            var service = CreateService(dbContext, provider);
            var organizationId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var submitted = await SubmitAsync(service, organizationId, actorId);

            var firstException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApproveApplicationAsync(
                    organizationId,
                    submitted.MerchantAccountId,
                    actorId,
                    CancellationToken.None));
            Assert.Contains("ambiguous", firstException.Message, StringComparison.OrdinalIgnoreCase);
            var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
            var originalKey = relationship.ApplicationCreateIdempotencyKey;
            provider.PayloadFingerprint = "PAYLOAD_FINGERPRINT_V2";

            var rotationException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApproveApplicationAsync(
                    organizationId,
                    submitted.MerchantAccountId,
                    actorId,
                    CancellationToken.None));

            Assert.Contains("definitive validation", rotationException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalKey, relationship.ApplicationCreateIdempotencyKey);
            Assert.Equal(1, provider.CreateCalls);
            Assert.Equal(0, provider.ReconciliationCalls);
        }
    }

    [Fact]
    public async Task Retrying_submission_reuses_the_persisted_idempotency_key()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider { FailFirstSubmit = true };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var submitted = await SubmitAsync(service, organizationId, actorId);
        await service.ApproveApplicationAsync(organizationId, submitted.MerchantAccountId, actorId, CancellationToken.None);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.CompleteLegalConsentAsync(
            organizationId,
            actorId,
            CancellationToken.None));
        var result = await service.CompleteLegalConsentAsync(organizationId, actorId, CancellationToken.None);
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();

        Assert.Equal(MerchantAccountStatuses.UnderReview, result.Status);
        Assert.Equal(2, provider.SubmitCalls);
        Assert.Equal(provider.SubmitIdempotencyKeys[0], provider.SubmitIdempotencyKeys[1]);
        Assert.Equal(relationship.ApplicationSubmitIdempotencyKey, provider.SubmitIdempotencyKeys[0]);
        Assert.NotNull(relationship.LegalConsentCompletedAtUtc);
        Assert.NotNull(relationship.ProviderApplicationSubmittedAtUtc);
    }

    [Fact]
    public async Task UnderReview_refresh_updates_status_timestamp_without_duplicate_records()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider { RefreshStatus = MerchantAccountStatuses.UnderReview };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var underReview = await SubmitApproveAndConsentAsync(service, organizationId, actorId);

        var result = await service.RefreshProviderStatusAsync(
            organizationId,
            underReview.MerchantAccountId,
            actorId,
            CancellationToken.None);
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();

        Assert.Equal(MerchantAccountStatuses.UnderReview, result.Status);
        Assert.Equal(1, provider.RefreshCalls);
        Assert.NotNull(relationship.ProviderStatusRefreshedAtUtc);
        Assert.Single(await dbContext.MerchantAccounts.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Credential_provisioning_retry_resumes_missing_key_and_activates_only_after_both_exist()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider { FailFirstTokenizationCredential = true };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var underReview = await SubmitApproveAndConsentAsync(service, organizationId, actorId);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.RefreshProviderStatusAsync(
            organizationId,
            underReview.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var accountAfterFailure = await dbContext.MerchantAccounts.IgnoreQueryFilters().SingleAsync();
        var relationshipAfterFailure = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(MerchantAccountStatuses.CredentialProvisioning, accountAfterFailure.Status);
        Assert.False(accountAfterFailure.PaymentsEnabled);
        Assert.Equal("Failed", relationshipAfterFailure.CredentialProvisioningStatus);
        Assert.NotNull(relationshipAfterFailure.PaymentApiKeyProtected);
        Assert.Null(relationshipAfterFailure.PublicTokenizationKeyProtected);

        var result = await service.RefreshProviderStatusAsync(
            organizationId,
            underReview.MerchantAccountId,
            actorId,
            CancellationToken.None);
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();

        Assert.Equal(MerchantAccountStatuses.Active, result.Status);
        Assert.Equal(1, provider.PaymentCredentialCalls);
        Assert.Equal(2, provider.TokenizationCredentialCalls);
        Assert.Single(provider.PaymentCredentialIdempotencyKeys.Distinct());
        Assert.Single(provider.TokenizationCredentialIdempotencyKeys.Distinct());
        Assert.Equal("Complete", relationship.CredentialProvisioningStatus);
        Assert.NotNull(relationship.CredentialsProvisionedAtUtc);
    }

    [Fact]
    public async Task Credential_timeout_requires_reconciliation_and_never_falsely_activates_merchant()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider { TimeoutFirstTokenizationCredential = true };
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var underReview = await SubmitApproveAndConsentAsync(service, organizationId, actorId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshProviderStatusAsync(
            organizationId,
            underReview.MerchantAccountId,
            actorId,
            CancellationToken.None));
        var account = await dbContext.MerchantAccounts.IgnoreQueryFilters().SingleAsync();
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();

        Assert.Contains("reconciliation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MerchantAccountStatuses.CredentialProvisioning, account.Status);
        Assert.False(account.PaymentsEnabled);
        Assert.Equal("ReconciliationRequired", relationship.CredentialProvisioningStatus);
        Assert.Equal(1, provider.PaymentCredentialCalls);
        Assert.Equal(1, provider.TokenizationCredentialCalls);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshProviderStatusAsync(
            organizationId,
            underReview.MerchantAccountId,
            actorId,
            CancellationToken.None));
        Assert.Equal(1, provider.PaymentCredentialCalls);
        Assert.Equal(1, provider.TokenizationCredentialCalls);
    }

    [Fact]
    public async Task Sensitive_application_and_provider_values_are_protected_and_absent_from_metadata()
    {
        await using var dbContext = CreateContext();
        var provider = new RecordingPartnerProvider();
        var service = CreateService(dbContext, provider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var underReview = await SubmitApproveAndConsentAsync(service, organizationId, actorId);
        await service.RefreshProviderStatusAsync(organizationId, underReview.MerchantAccountId, actorId, CancellationToken.None);

        var account = await dbContext.MerchantAccounts.IgnoreQueryFilters().SingleAsync();
        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
        AssertProtected(account.TaxIdentifierProtected, "821234567");
        AssertProtected(account.OwnerDateOfBirthProtected, "1980-01-01");
        AssertProtected(account.OwnerSsnProtected, "111223333");
        AssertProtected(account.BankRoutingNumberProtected, "111000025");
        AssertProtected(account.BankAccountNumberProtected, "1234567890");
        Assert.Equal("****4567", account.Ein);
        Assert.Equal("3333", account.OwnerSsnLastFour);
        AssertProtected(relationship.GatewayPasswordProtected, "gateway-password-secret");
        AssertProtected(relationship.PaymentApiKeyProtected, "private-transaction-secret");
        AssertProtected(relationship.PublicTokenizationKeyProtected, "public-tokenization-secret");
        AssertProtected(relationship.LegalConsentUrlProtected, "https://secure.nmi.com/consent/application-123");

        var persistedSafeText = string.Join('|', relationship.CapabilitiesJson, relationship.CredentialMetadataJson, relationship.LastProviderError);
        Assert.DoesNotContain("raw-provider-secret", persistedSafeText, StringComparison.Ordinal);
        Assert.DoesNotContain("gateway-password-secret", persistedSafeText, StringComparison.Ordinal);
        Assert.DoesNotContain("private-transaction-secret", persistedSafeText, StringComparison.Ordinal);
        Assert.DoesNotContain("public-tokenization-secret", persistedSafeText, StringComparison.Ordinal);
        Assert.DoesNotContain("821234567", persistedSafeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_onboarding_to_payment_workflow_persists_visible_transaction()
    {
        await using var dbContext = CreateContext();
        var partnerProvider = new RecordingPartnerProvider();
        var merchantService = CreateService(dbContext, partnerProvider);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var underReview = await SubmitApproveAndConsentAsync(merchantService, organizationId, actorId);
        await merchantService.RefreshProviderStatusAsync(
            organizationId,
            underReview.MerchantAccountId,
            actorId,
            CancellationToken.None);
        var paymentProvider = new RecordingPaymentProvider();
        var paymentService = new PaymentService(
            dbContext,
            paymentProvider,
            new DomainEventRecorder(dbContext, new NoCurrentUser(), new SystemClock()),
            new SystemClock(),
            SecretProtector);

        var payment = await paymentService.CreateSaleAsync(
            organizationId,
            actorId,
            new PaymentSaleRequest("hosted-fields-token", 42.50m, OrderId: "ORDER-100"),
            CancellationToken.None);
        var workspace = await paymentService.GetWorkspaceAsync(organizationId, CancellationToken.None);

        Assert.True(payment.Success);
        Assert.Equal("sandbox-transaction-123", payment.GatewayTransactionId);
        Assert.Equal("private-transaction-secret", paymentProvider.Request?.PaymentApiKey);
        Assert.Equal("hosted-fields-token", paymentProvider.Request?.PaymentToken);
        Assert.Single(workspace.Transactions);
        Assert.Equal("Approved", workspace.Transactions.Single().Status);
        Assert.Equal(42.50m, workspace.ApprovedSalesTotal);
    }

    private static void AssertProtected(string? protectedValue, string plaintext)
    {
        Assert.NotNull(protectedValue);
        Assert.NotEqual(plaintext, protectedValue);
        Assert.DoesNotContain(plaintext, protectedValue, StringComparison.Ordinal);
        Assert.Equal(plaintext, SecretProtector.Unprotect(protectedValue));
    }

    private static NmiValidationException ValidationFailure()
    {
        return new NmiValidationException(
            System.Net.HttpStatusCode.UnprocessableEntity,
            ["fld_federal_tax_id"],
            ["invalid_format"],
            ["Value format is invalid."]);
    }

    private static async Task<MerchantAccountResult> SubmitAsync(
        MerchantAccountService service,
        Guid organizationId,
        Guid actorId)
    {
        return await service.SubmitApplicationAsync(
            organizationId,
            actorId,
            ApplicationRequest(),
            CancellationToken.None);
    }

    private static async Task<MerchantAccountResult> SubmitApproveAndConsentAsync(
        MerchantAccountService service,
        Guid organizationId,
        Guid actorId)
    {
        var submitted = await SubmitAsync(service, organizationId, actorId);
        await service.ApproveApplicationAsync(
            organizationId,
            submitted.MerchantAccountId,
            actorId,
            CancellationToken.None);
        return await service.CompleteLegalConsentAsync(organizationId, actorId, CancellationToken.None);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new SystemClock(), new TenantProvider(currentUser));
    }

    private static MerchantAccountService CreateService(ApplicationDbContext dbContext, RecordingPartnerProvider provider)
    {
        return new MerchantAccountService(
            dbContext,
            new[] { provider },
            new DomainEventRecorder(dbContext, new NoCurrentUser(), new SystemClock()),
            new SystemClock(),
            SecretProtector);
    }

    private static MerchantApplicationRequest ApplicationRequest()
    {
        return new MerchantApplicationRequest(
            BusinessName: "iM1 Test Shop",
            Dba: "iM1 Test",
            Ein: null,
            TaxId: "821234567",
            BusinessType: "LLC",
            BusinessDescription: "Motorcycle parts and service",
            YearsInBusiness: 5,
            PhysicalAddressLine1: "100 Main St",
            PhysicalAddressLine2: null,
            PhysicalCity: "Dallas",
            PhysicalRegion: "TX",
            PhysicalPostalCode: "75001",
            PhysicalCountry: "US",
            MailingAddressLine1: null,
            MailingAddressLine2: null,
            MailingCity: null,
            MailingRegion: null,
            MailingPostalCode: null,
            MailingCountry: null,
            OwnerName: "Bradley Molen",
            OwnerEmail: "brad@example.test",
            OwnerPhone: "2145551212",
            OwnerTitle: "Owner",
            OwnerOwnershipPercentage: 100m,
            OwnerDateOfBirth: "1980-01-01",
            OwnerSsn: "111223333",
            BankName: "Test Bank",
            BankRoutingNumber: "111000025",
            BankAccountNumber: "1234567890",
            ExpectedMonthlyVolume: 5000m,
            AverageTicket: 100m,
            HighTicket: 250m,
            CardPresentPercentage: 100m,
            KeyEnteredPercentage: 0m,
            EcommercePercentage: 0m,
            MotoPercentage: 0m,
            Website: "https://example.test",
            Mcc: "5533");
    }

    private sealed class RecordingPartnerProvider : IPartnerProvider
    {
        public bool FailFirstCreate { get; init; }
        public bool FailFirstSubmit { get; init; }
        public bool FailFirstTokenizationCredential { get; init; }
        public bool TimeoutFirstTokenizationCredential { get; init; }
        public Exception? CreateException { get; init; }
        public Exception? FirstCreateException { get; init; }
        public string PayloadFingerprint { get; set; } = "PAYLOAD_FINGERPRINT_V1";
        public bool MatchingApplicationExists { get; set; }
        public string RefreshStatus { get; init; } = MerchantAccountStatuses.Active;
        public int CreateCalls { get; private set; }
        public int SubmitCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public int PaymentCredentialCalls { get; private set; }
        public int TokenizationCredentialCalls { get; private set; }
        public int ReconciliationCalls { get; private set; }
        public List<string> CreateIdempotencyKeys { get; } = [];
        public List<string> SubmitIdempotencyKeys { get; } = [];
        public List<string> PaymentCredentialIdempotencyKeys { get; } = [];
        public List<string> TokenizationCredentialIdempotencyKeys { get; } = [];

        public string ProviderCode => "NMI";

        public FinancialProviderConfiguration GetConfiguration() => new(
            true,
            ProviderCode,
            "Sandbox",
            null,
            "https://secure.nmi.com/token/Collect.js",
            true,
            true);

        public Task<PartnerMerchantCreateResult> CreateMerchantAsync(
            PartnerMerchantCreateRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            CreateIdempotencyKeys.Add(idempotencyKey);
            if (FirstCreateException is not null && CreateCalls == 1)
            {
                throw FirstCreateException;
            }
            if (CreateException is not null)
            {
                throw CreateException;
            }
            if (FailFirstCreate && CreateCalls == 1)
            {
                throw new HttpRequestException("Simulated NMI application timeout.");
            }

            return Task.FromResult(Result(
                MerchantAccountStatuses.LegalConsentRequired,
                providerMerchantId: string.Empty,
                legalConsentUrl: "https://secure.nmi.com/consent/application-123"));
        }

        public string GetMerchantApplicationPayloadFingerprint(PartnerMerchantCreateRequest request) =>
            PayloadFingerprint;

        public Task<bool> HasMatchingMerchantApplicationAsync(
            PartnerMerchantCreateRequest request,
            CancellationToken cancellationToken)
        {
            ReconciliationCalls++;
            return Task.FromResult(MatchingApplicationExists);
        }

        public Task<PartnerMerchantCreateResult> SubmitMerchantApplicationAsync(
            string providerReference,
            PartnerMerchantCreateRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            SubmitCalls++;
            SubmitIdempotencyKeys.Add(idempotencyKey);
            if (FailFirstSubmit && SubmitCalls == 1)
            {
                throw new HttpRequestException("Simulated NMI submission timeout.");
            }

            return Task.FromResult(Result(MerchantAccountStatuses.UnderReview, providerMerchantId: string.Empty));
        }

        public Task<PartnerMerchantCreateResult> GetMerchantApplicationStatusAsync(
            string providerReference,
            CancellationToken cancellationToken)
        {
            RefreshCalls++;
            var merchantId = RefreshStatus == MerchantAccountStatuses.Active ? "nmi-merchant-123" : string.Empty;
            return Task.FromResult(Result(RefreshStatus, merchantId));
        }

        public Task<PartnerMerchantCredentialResult> CreateMerchantCredentialAsync(
            PartnerMerchantCredentialRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var tokenization = request.Permissions.Contains("tokenization", StringComparer.OrdinalIgnoreCase);
            if (tokenization)
            {
                TokenizationCredentialCalls++;
                TokenizationCredentialIdempotencyKeys.Add(idempotencyKey);
                if (FailFirstTokenizationCredential && TokenizationCredentialCalls == 1)
                {
                    throw new HttpRequestException(
                        "Simulated credential validation failure.",
                        null,
                        System.Net.HttpStatusCode.BadRequest);
                }
                if (TimeoutFirstTokenizationCredential && TokenizationCredentialCalls == 1)
                {
                    throw new TaskCanceledException("Simulated ambiguous credential timeout.");
                }

                return Task.FromResult(new PartnerMerchantCredentialResult(
                    "tokenization-key-id",
                    null,
                    "public-tokenization-secret",
                    request.Description,
                    "raw-provider-secret-public-tokenization-secret"));
            }

            PaymentCredentialCalls++;
            PaymentCredentialIdempotencyKeys.Add(idempotencyKey);
            return Task.FromResult(new PartnerMerchantCredentialResult(
                "transaction-key-id",
                "private-transaction-secret",
                null,
                request.Description,
                "raw-provider-secret-private-transaction-secret"));
        }

        private PartnerMerchantCreateResult Result(
            string status,
            string providerMerchantId,
            string? legalConsentUrl = null)
        {
            return new PartnerMerchantCreateResult(
                ProviderCode,
                providerMerchantId,
                "gateway-user",
                "gateway-password-secret",
                status,
                "raw-provider-secret-gateway-password-secret-821234567",
                "application-123",
                legalConsentUrl);
        }
    }

    private sealed class RecordingPaymentProvider : IPaymentProvider
    {
        public ProviderPaymentSaleRequest? Request { get; private set; }
        public string ProviderCode => "NMI";

        public FinancialProviderConfiguration GetConfiguration() => new(
            true,
            ProviderCode,
            "Sandbox",
            null,
            "https://secure.nmi.com/token/Collect.js",
            false,
            true);

        public Task<ProviderPaymentResult> ProcessSaleAsync(
            ProviderPaymentSaleRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ProviderPaymentResult(
                true,
                "Approved",
                "sandbox-transaction-123",
                "AUTH123",
                "1",
                "Approved",
                "visa",
                "1111",
                "raw-card-sensitive-response"));
        }
    }
}
