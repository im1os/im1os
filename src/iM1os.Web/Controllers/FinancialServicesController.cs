using System.Security.Claims;
using iM1os.Application.FinancialServices;
using iM1os.Application.FinancialServices.Merchant;
using iM1os.Web.Development;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class FinancialServicesController(
    IMerchantAccountService merchantAccountService,
    IWebHostEnvironment hostEnvironment) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(FinancialServicesModuleCatalog.Workspace());
    }

    [HttpGet]
    public IActionResult Module(string moduleKey)
    {
        return ModuleFor(moduleKey);
    }

    [HttpGet]
    public async Task<IActionResult> MerchantAccount(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        return View(await merchantAccountService.GetCompanyMerchantAccountAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> MerchantApplication(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        return View("MerchantAccount", await merchantAccountService.GetCompanyMerchantAccountAsync(organizationId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMerchantApplication(MerchantApplicationForm form, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await merchantAccountService.SaveDraftAsync(organizationId, UserId(), form.ToRequest(), cancellationToken);
            TempData["MerchantStatus"] = "Merchant application saved.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["MerchantError"] = ex.Message;
        }

        return RedirectToAction(nameof(MerchantApplication), new { organizationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Owner,Administrator")]
    public async Task<IActionResult> FillMerchantApplicationSandboxData(CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return NotFound();
        }

        var organizationId = OrganizationId();
        try
        {
            await merchantAccountService.SaveDraftAsync(
                organizationId,
                UserId(),
                NmiSandboxMerchantApplicationFixture.Create().ToRequest(),
                cancellationToken);
            TempData["MerchantStatus"] = "Development-only NMI sandbox test data saved.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["MerchantError"] = ex.Message;
        }

        return RedirectToAction(nameof(MerchantApplication), new { organizationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitMerchantApplication(MerchantApplicationForm form, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await merchantAccountService.SubmitApplicationAsync(organizationId, UserId(), form.ToRequest(), cancellationToken);
            TempData["MerchantStatus"] = "Merchant application submitted for platform review.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["MerchantError"] = ex.Message;
        }

        return RedirectToAction(nameof(MerchantApplication), new { organizationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Owner,Administrator")]
    public async Task<IActionResult> CompleteNmiLegalConsent(CancellationToken cancellationToken)
    {
        var organizationId = CompanyOrganizationId();
        try
        {
            var result = await merchantAccountService.CompleteLegalConsentAsync(
                organizationId,
                UserId(),
                cancellationToken);
            TempData["MerchantStatus"] = result.Status switch
            {
                "UnderReview" => "Legal consent completed. The application was submitted to NMI and is under review.",
                "CredentialProvisioning" => "NMI approved the application. Secure payment credentials are being provisioned.",
                "Active" => "NMI approved the merchant and payments are active.",
                "Rejected" => "NMI declined the merchant application.",
                _ => $"Legal consent recorded. Merchant status: {result.Status}."
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            TempData["MerchantError"] = ex.Message;
        }

        return RedirectToAction(nameof(MerchantApplication), new { organizationId });
    }

    [HttpGet]
    public IActionResult TestPayment()
    {
        return RedirectToAction("Index", "Payments");
    }

    [HttpGet]
    public IActionResult TerminalManagement()
    {
        return ModuleFor("terminal-management");
    }

    [HttpGet]
    public IActionResult TransactionCenter()
    {
        return ModuleFor("transaction-center");
    }

    [HttpGet]
    public IActionResult CustomerWallet()
    {
        return ModuleFor("customer-wallet");
    }

    [HttpGet]
    public IActionResult Deposits()
    {
        return ModuleFor("deposits");
    }

    [HttpGet]
    public IActionResult Statements()
    {
        return ModuleFor("statements");
    }

    [HttpGet]
    public IActionResult Reports()
    {
        return ModuleFor("reports");
    }

    [HttpGet]
    public IActionResult Settings()
    {
        return ModuleFor("settings");
    }

    [HttpGet]
    public IActionResult PaymentLinks()
    {
        return ModuleFor("payment-links");
    }

    [HttpGet]
    public IActionResult AchProcessing()
    {
        return ModuleFor("ach-processing");
    }

    [HttpGet]
    public IActionResult SubscriptionBilling()
    {
        return ModuleFor("subscription-billing");
    }

    [HttpGet]
    public IActionResult FinancialLedger()
    {
        return ModuleFor("financial-ledger");
    }

    private IActionResult ModuleFor(string moduleKey)
    {
        var module = FinancialServicesModuleCatalog.Find(moduleKey);
        if (module is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(module.Controller) && !string.IsNullOrWhiteSpace(module.Action))
        {
            return RedirectToAction(module.Action, module.Controller);
        }

        return View(module);
    }

    private bool TryOrganizationId(out Guid organizationId)
    {
        organizationId = Guid.Empty;
        return User.FindFirstValue("platform_user_id") is null &&
            Guid.TryParse(User.FindFirstValue("organization_id"), out organizationId);
    }

    private Guid OrganizationId()
    {
        return TryOrganizationId(out var organizationId)
            ? organizationId
            : throw new UnauthorizedAccessException("An organization context is required.");
    }

    private Guid CompanyOrganizationId()
    {
        if (User.FindFirstValue("platform_user_id") is not null ||
            !Guid.TryParse(User.FindFirstValue("organization_id"), out var organizationId))
        {
            throw new UnauthorizedAccessException("A company signer context is required.");
        }

        return organizationId;
    }

    private Guid UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("platform_user_id");

        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A user context is required.");
    }

    private IActionResult RedirectToMissingOrganizationContext()
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction("Tenants", "Platform")
            : RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }

}

public sealed class MerchantApplicationForm
{
    public string BusinessName { get; set; } = string.Empty;

    public string? Dba { get; set; }

    public string? Ein { get; set; }

    public string? TaxId { get; set; }

    public string? BusinessType { get; set; }

    public string? BusinessDescription { get; set; }

    public int? YearsInBusiness { get; set; }

    public string PhysicalAddressLine1 { get; set; } = string.Empty;

    public string? PhysicalAddressLine2 { get; set; }

    public string PhysicalCity { get; set; } = string.Empty;

    public string PhysicalRegion { get; set; } = string.Empty;

    public string PhysicalPostalCode { get; set; } = string.Empty;

    public string PhysicalCountry { get; set; } = "US";

    public string? MailingAddressLine1 { get; set; }

    public string? MailingAddressLine2 { get; set; }

    public string? MailingCity { get; set; }

    public string? MailingRegion { get; set; }

    public string? MailingPostalCode { get; set; }

    public string? MailingCountry { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public string OwnerEmail { get; set; } = string.Empty;

    public string OwnerPhone { get; set; } = string.Empty;

    public string? OwnerTitle { get; set; }

    public decimal? OwnerOwnershipPercentage { get; set; }

    public string? OwnerDateOfBirth { get; set; }

    public string? OwnerSsn { get; set; }

    public string? BankName { get; set; }

    public string? BankRoutingNumber { get; set; }

    public string? BankAccountNumber { get; set; }

    public decimal? ExpectedMonthlyVolume { get; set; }

    public decimal? AverageTicket { get; set; }

    public decimal? HighTicket { get; set; }

    public decimal? CardPresentPercentage { get; set; }

    public decimal? KeyEnteredPercentage { get; set; }

    public decimal? EcommercePercentage { get; set; }

    public decimal? MotoPercentage { get; set; }

    public string? Website { get; set; }

    public string? Mcc { get; set; }

    public MerchantApplicationRequest ToRequest()
    {
        return new MerchantApplicationRequest(
            BusinessName,
            Dba,
            Ein,
            TaxId,
            BusinessType,
            BusinessDescription,
            YearsInBusiness,
            PhysicalAddressLine1,
            PhysicalAddressLine2,
            PhysicalCity,
            PhysicalRegion,
            PhysicalPostalCode,
            PhysicalCountry,
            MailingAddressLine1,
            MailingAddressLine2,
            MailingCity,
            MailingRegion,
            MailingPostalCode,
            MailingCountry,
            OwnerName,
            OwnerEmail,
            OwnerPhone,
            OwnerTitle,
            OwnerOwnershipPercentage,
            OwnerDateOfBirth,
            OwnerSsn,
            BankName,
            BankRoutingNumber,
            BankAccountNumber,
            ExpectedMonthlyVolume,
            AverageTicket,
            HighTicket,
            CardPresentPercentage,
            KeyEnteredPercentage,
            EcommercePercentage,
            MotoPercentage,
            Website,
            Mcc);
    }
}
