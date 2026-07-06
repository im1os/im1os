using iM1os.Application.FinancialServices.Merchant;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Domain.FinancialServices.Events;
using iM1os.Domain.FinancialServices.Merchant;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class MerchantAccountServiceTests
{
    [Fact]
    public async Task OnboardMerchantAsync_creates_merchant_application_and_submission_event()
    {
        await using var dbContext = CreateContext();
        var partnerProvider = new RecordingPartnerProvider();
        var service = new MerchantAccountService(
            dbContext,
            new[] { partnerProvider },
            new DomainEventRecorder(dbContext, new NoCurrentUser(), new SystemClock()),
            new SystemClock());
        var organizationId = Guid.NewGuid();

        var result = await service.OnboardMerchantAsync(
            organizationId,
            Guid.NewGuid(),
            new MerchantOnboardingRequest(
                "iM1 Test Shop",
                "US",
                "100 Main St",
                null,
                "Dallas",
                "TX",
                "75001",
                "America/Chicago",
                "Bradley",
                "Molen",
                "brad@example.test",
                "555-0100",
                "https://example.test"),
            CancellationToken.None);

        Assert.Equal("NMI", result.ProviderCode);
        Assert.Equal(string.Empty, result.ProviderMerchantId);
        Assert.Equal(MerchantAccountStatuses.Submitted, result.Status);
        Assert.Null(partnerProvider.Request);

        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(MerchantAccountStatuses.Submitted, merchantAccount.Status);
        Assert.Equal("Submitted", merchantAccount.UnderwritingStatus);
        Assert.Equal("NMI", merchantAccount.PrimaryProviderCode);

        Assert.Empty(await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().ToListAsync());

        var histories = await dbContext.MerchantAccountStatusHistories.IgnoreQueryFilters()
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();
        Assert.Collection(
            histories,
            x =>
            {
                Assert.Null(x.OldStatus);
                Assert.Equal(MerchantAccountStatuses.Draft, x.NewStatus);
            },
            x =>
            {
                Assert.Equal(MerchantAccountStatuses.Draft, x.OldStatus);
                Assert.Equal(MerchantAccountStatuses.Submitted, x.NewStatus);
                Assert.Null(x.ProviderReference);
            });

        var domainEvent = await dbContext.DomainEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(FinancialEventTypes.MerchantApplicationSubmitted, domainEvent.EventType);
        Assert.Equal("FinancialServices", domainEvent.SourceModule);
        Assert.Contains(MerchantAccountStatuses.Submitted, domainEvent.PayloadJson);
    }

    [Fact]
    public async Task ApproveApplicationAsync_creates_provider_relationship_credentials_status_history_and_events()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var result = await service.OnboardMerchantAsync(organizationId, actorId, OnboardingRequest(), CancellationToken.None);

        await service.ApproveApplicationAsync(organizationId, result.MerchantAccountId, actorId, CancellationToken.None);

        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(MerchantAccountStatuses.Active, merchantAccount.Status);
        Assert.True(merchantAccount.PaymentsEnabled);

        var relationship = await dbContext.MerchantProviderRelationships.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(merchantAccount.Id, relationship.MerchantAccountId);
        Assert.Equal("nmi-gateway-123", relationship.ProviderMerchantId);
        Assert.Equal(MerchantAccountStatuses.Active, relationship.Status);
        Assert.Equal("gateway-user", relationship.GatewayUsername);
        Assert.Equal("gateway-pass", relationship.GatewayPasswordProtected);
        Assert.Equal("private_txn_key", relationship.PaymentApiKeyProtected);
        Assert.Equal("public_token_key", relationship.PublicTokenizationKey);

        var statuses = await dbContext.MerchantAccountStatusHistories.IgnoreQueryFilters()
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.NewStatus)
            .ToListAsync();
        Assert.Equal(
            new[]
            {
                MerchantAccountStatuses.Draft,
                MerchantAccountStatuses.Submitted,
                MerchantAccountStatuses.Approved,
                MerchantAccountStatuses.Active
            },
            statuses);

        var eventTypes = await dbContext.DomainEvents.IgnoreQueryFilters()
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => x.EventType)
            .ToListAsync();
        Assert.Contains(FinancialEventTypes.MerchantApplicationSubmitted, eventTypes);
        Assert.Contains(FinancialEventTypes.MerchantApproved, eventTypes);
        Assert.Contains(FinancialEventTypes.MerchantActivated, eventTypes);
    }

    [Fact]
    public async Task Company_workspace_only_returns_requested_company_merchant_account()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        await service.OnboardMerchantAsync(firstOrganizationId, Guid.NewGuid(), OnboardingRequest("First Shop"), CancellationToken.None);
        await service.OnboardMerchantAsync(secondOrganizationId, Guid.NewGuid(), OnboardingRequest("Second Shop"), CancellationToken.None);

        var workspace = await service.GetCompanyMerchantAccountAsync(firstOrganizationId, CancellationToken.None);

        Assert.Equal(firstOrganizationId, workspace.OrganizationId);
        Assert.NotNull(workspace.Account);
        Assert.Equal("First Shop", workspace.Account.LegalBusinessName);
        Assert.DoesNotContain(workspace.StatusHistory, x => x.CompanyId == secondOrganizationId);
    }

    [Fact]
    public async Task Platform_workspaces_show_platform_merchant_management_data()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();

        await service.OnboardMerchantAsync(firstOrganizationId, Guid.NewGuid(), OnboardingRequest("First Shop"), CancellationToken.None);
        var active = await service.OnboardMerchantAsync(secondOrganizationId, Guid.NewGuid(), OnboardingRequest("Second Shop"), CancellationToken.None);
        await service.ApproveApplicationAsync(secondOrganizationId, active.MerchantAccountId, Guid.NewGuid(), CancellationToken.None);

        var applications = await service.GetPlatformMerchantApplicationsAsync(CancellationToken.None);
        var activeMerchants = await service.GetPlatformActiveMerchantsAsync(CancellationToken.None);

        Assert.Contains(applications.Applications, x =>
            x.CompanyId == firstOrganizationId &&
            string.IsNullOrWhiteSpace(x.ProviderMerchantId) &&
            x.StatusHistory.Any());
        Assert.DoesNotContain(applications.Applications, x => x.CompanyId == secondOrganizationId);
        Assert.Contains(activeMerchants.Merchants, x =>
            x.CompanyId == secondOrganizationId &&
            x.ProviderMerchantId == "nmi-gateway-123" &&
            x.Status == MerchantAccountStatuses.Active);
        Assert.DoesNotContain(activeMerchants.Merchants, x => x.CompanyId == firstOrganizationId);
    }

    [Fact]
    public async Task SaveDraftAsync_updates_submitted_application_without_downgrading_status()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await service.OnboardMerchantAsync(organizationId, actorId, OnboardingRequest("Original Shop"), CancellationToken.None);

        var result = await service.SaveDraftAsync(
            organizationId,
            actorId,
            ApplicationRequest("Updated Shop"),
            CancellationToken.None);

        Assert.Equal(MerchantAccountStatuses.Submitted, result.Status);
        var merchantAccount = await dbContext.MerchantAccounts.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(MerchantAccountStatuses.Submitted, merchantAccount.Status);
        Assert.Equal("Updated Shop", merchantAccount.LegalBusinessName);

        var statuses = await dbContext.MerchantAccountStatusHistories.IgnoreQueryFilters()
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.NewStatus)
            .ToListAsync();
        Assert.Equal(
            new[] { MerchantAccountStatuses.Draft, MerchantAccountStatuses.Submitted },
            statuses);
    }

    [Fact]
    public async Task SaveDraftAsync_rejects_active_application_edits()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var result = await service.OnboardMerchantAsync(organizationId, actorId, OnboardingRequest(), CancellationToken.None);
        await service.ApproveApplicationAsync(organizationId, result.MerchantAccountId, actorId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveDraftAsync(
                organizationId,
                actorId,
                ApplicationRequest("Should Not Save"),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetPlatformMerchantApplicationAsync_returns_review_detail()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);
        var organizationId = Guid.NewGuid();
        var result = await service.OnboardMerchantAsync(organizationId, Guid.NewGuid(), OnboardingRequest("Review Shop"), CancellationToken.None);

        var detail = await service.GetPlatformMerchantApplicationAsync(
            organizationId,
            result.MerchantAccountId,
            CancellationToken.None);

        Assert.Equal(organizationId, detail.Merchant.CompanyId);
        Assert.Equal(result.MerchantAccountId, detail.Merchant.MerchantAccountId);
        Assert.Equal("Review Shop", detail.Application.BusinessName);
        Assert.NotEmpty(detail.Merchant.StatusHistory);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new SystemClock(), new TenantProvider(currentUser));
    }

    private static MerchantAccountService CreateService(ApplicationDbContext dbContext)
    {
        return new MerchantAccountService(
            dbContext,
            new[] { new RecordingPartnerProvider() },
            new DomainEventRecorder(dbContext, new NoCurrentUser(), new SystemClock()),
            new SystemClock());
    }

    private static MerchantOnboardingRequest OnboardingRequest(string legalBusinessName = "iM1 Test Shop")
    {
        return new MerchantOnboardingRequest(
            legalBusinessName,
            "US",
            "100 Main St",
            null,
            "Dallas",
            "TX",
            "75001",
            "America/Chicago",
            "Bradley",
            "Molen",
            "brad@example.test",
            "555-0100",
            "https://example.test");
    }

    private static MerchantApplicationRequest ApplicationRequest(string businessName = "iM1 Test Shop")
    {
        return new MerchantApplicationRequest(
            businessName,
            null,
            null,
            null,
            "LLC",
            "100 Main St",
            null,
            "Dallas",
            "TX",
            "75001",
            "US",
            null,
            null,
            null,
            null,
            null,
            null,
            "Bradley Molen",
            "brad@example.test",
            "2145551212",
            "Test Bank",
            "123456789",
            "1234567890",
            5000,
            100,
            "https://example.test",
            "5533");
    }

    private sealed class RecordingPartnerProvider : IPartnerProvider
    {
        public PartnerMerchantCreateRequest? Request { get; private set; }

        public string ProviderCode => "NMI";

        public FinancialProviderConfiguration GetConfiguration()
        {
            return new FinancialProviderConfiguration(
                true,
                ProviderCode,
                "Sandbox",
                "public-tokenization-key",
                "https://secure.nmi.com/token/Collect.js",
                true,
                true);
        }

        public Task<PartnerMerchantCreateResult> CreateMerchantAsync(
            PartnerMerchantCreateRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new PartnerMerchantCreateResult(
                ProviderCode,
                "nmi-gateway-123",
                "gateway-user",
                "gateway-pass",
                "Active",
                    """{"gateway_id":"nmi-gateway-123"}"""));
        }

        public Task<PartnerMerchantCreateResult> SubmitMerchantApplicationAsync(
            string providerReference,
            PartnerMerchantCreateRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new PartnerMerchantCreateResult(
                ProviderCode,
                "nmi-gateway-123",
                "gateway-user",
                "gateway-pass",
                "Active",
                """{"gateway_id":"nmi-gateway-123"}""",
                providerReference));
        }

        public Task<PartnerMerchantCreateResult> GetMerchantApplicationStatusAsync(
            string providerReference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PartnerMerchantCreateResult(
                ProviderCode,
                "nmi-gateway-123",
                "gateway-user",
                "gateway-pass",
                "Active",
                """{"status":"boarded","gateway_id":"nmi-gateway-123"}""",
                providerReference));
        }

        public Task<PartnerMerchantCredentialResult> CreateMerchantCredentialAsync(
            PartnerMerchantCredentialRequest request,
            CancellationToken cancellationToken)
        {
            var isTokenizationKey = request.Permissions.Contains("tokenization", StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new PartnerMerchantCredentialResult(
                isTokenizationKey ? "tokenization-key-id" : "transaction-key-id",
                isTokenizationKey ? null : "private_txn_key",
                isTokenizationKey ? "public_token_key" : null,
                request.Description,
                isTokenizationKey
                    ? """{"key":"public_token_key"}"""
                    : """{"key":"private_txn_key"}"""));
        }
    }
}
