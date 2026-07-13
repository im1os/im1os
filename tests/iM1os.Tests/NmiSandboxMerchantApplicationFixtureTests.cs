using System.Security.Claims;
using iM1os.Application.FinancialServices.Merchant;
using iM1os.Web.Controllers;
using iM1os.Web.Development;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace iM1os.Tests;

public sealed class NmiSandboxMerchantApplicationFixtureTests
{
    [Fact]
    public void Create_ReturnsCompleteSyntheticApplicationInNmiApprovalBand()
    {
        var form = NmiSandboxMerchantApplicationFixture.Create();
        var request = form.ToRequest();

        Assert.Contains("Sandbox", request.BusinessName, StringComparison.Ordinal);
        Assert.EndsWith("@example.com", request.OwnerEmail, StringComparison.Ordinal);
        Assert.Equal("https://example.com", request.Website);
        Assert.Equal("5533", request.Mcc);
        Assert.False(string.IsNullOrWhiteSpace(request.TaxId));
        Assert.False(string.IsNullOrWhiteSpace(request.OwnerDateOfBirth));
        Assert.False(string.IsNullOrWhiteSpace(request.OwnerSsn));
        Assert.False(string.IsNullOrWhiteSpace(request.BankRoutingNumber));
        Assert.False(string.IsNullOrWhiteSpace(request.BankAccountNumber));
        Assert.InRange(request.ExpectedMonthlyVolume!.Value, 5000m, 99999.99m);
        Assert.Equal(100m, request.CardPresentPercentage + request.KeyEnteredPercentage +
            request.EcommercePercentage + request.MotoPercentage);
    }

    [Fact]
    public async Task FillSandboxData_ReturnsNotFoundOutsideDevelopmentWithoutSaving()
    {
        var service = new RecordingMerchantAccountService();
        var controller = CreateController(service, Environments.Production);

        var result = await controller.FillMerchantApplicationSandboxData(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(service.SavedRequest);
    }

    [Fact]
    public async Task FillSandboxData_SavesFixtureForCompanyInDevelopment()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var service = new RecordingMerchantAccountService();
        var controller = CreateController(service, Environments.Development, organizationId, actorUserId);

        var result = await controller.FillMerchantApplicationSandboxData(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FinancialServicesController.MerchantApplication), redirect.ActionName);
        Assert.Equal(organizationId, service.SavedOrganizationId);
        Assert.Equal(actorUserId, service.SavedActorUserId);
        Assert.Equal("NMI Sandbox Motorcycle Supply LLC", service.SavedRequest?.BusinessName);
        Assert.Equal("Development-only NMI sandbox test data saved.", controller.TempData["MerchantStatus"]);
    }

    [Fact]
    public async Task FillSandboxData_SavesWhenExplicitlyEnabledForDevDeployment()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var service = new RecordingMerchantAccountService();
        var controller = CreateController(
            service,
            Environments.Production,
            organizationId,
            actorUserId,
            enableSandboxFixture: true);

        var result = await controller.FillMerchantApplicationSandboxData(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(service.SavedRequest);
    }

    private static FinancialServicesController CreateController(
        IMerchantAccountService service,
        string environmentName,
        Guid? organizationId = null,
        Guid? actorUserId = null,
        bool enableSandboxFixture = false)
    {
        var claims = new List<Claim>();
        if (organizationId.HasValue)
        {
            claims.Add(new Claim("organization_id", organizationId.Value.ToString()));
        }

        if (actorUserId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, actorUserId.Value.ToString()));
        }

        claims.Add(new Claim(ClaimTypes.Role, "Owner"));
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NmiPayments:EnableSandboxApplicationFixture"] = enableSandboxFixture.ToString()
            })
            .Build();
        var controller = new FinancialServicesController(
            service,
            new TestWebHostEnvironment(environmentName),
            configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "iM1os.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> values = new(StringComparer.Ordinal);

        public IDictionary<string, object> LoadTempData(HttpContext context) => values;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            this.values = new Dictionary<string, object>(values, StringComparer.Ordinal);
        }
    }

    private sealed class RecordingMerchantAccountService : IMerchantAccountService
    {
        public Guid? SavedOrganizationId { get; private set; }
        public Guid? SavedActorUserId { get; private set; }
        public MerchantApplicationRequest? SavedRequest { get; private set; }

        public Task<MerchantAccountResult> SaveDraftAsync(
            Guid organizationId,
            Guid actorUserId,
            MerchantApplicationRequest request,
            CancellationToken cancellationToken)
        {
            SavedOrganizationId = organizationId;
            SavedActorUserId = actorUserId;
            SavedRequest = request;
            return Task.FromResult(new MerchantAccountResult(
                Guid.NewGuid(), organizationId, "Draft", "NotSubmitted", "NMI", string.Empty));
        }

        public Task<CompanyMerchantAccountWorkspace> GetCompanyMerchantAccountAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlatformMerchantApplicationsWorkspace> GetPlatformMerchantApplicationsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlatformMerchantApplicationDetail> GetPlatformMerchantApplicationAsync(Guid organizationId, Guid merchantAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PlatformActiveMerchantsWorkspace> GetPlatformActiveMerchantsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> OnboardMerchantAsync(Guid organizationId, Guid actorUserId, MerchantOnboardingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> SubmitApplicationAsync(Guid organizationId, Guid actorUserId, MerchantApplicationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> ApproveApplicationAsync(Guid organizationId, Guid merchantAccountId, Guid actorUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> RefreshProviderStatusAsync(Guid organizationId, Guid merchantAccountId, Guid actorUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> CompleteLegalConsentAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> RejectApplicationAsync(Guid organizationId, Guid merchantAccountId, Guid actorUserId, string? reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MerchantAccountResult> ChangeStatusAsync(Guid organizationId, Guid merchantAccountId, Guid actorUserId, string newStatus, string? reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
