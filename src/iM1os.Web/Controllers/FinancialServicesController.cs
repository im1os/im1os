using System.Security.Claims;
using iM1os.Application.FinancialServices;
using iM1os.Application.FinancialServices.Merchant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class FinancialServicesController(IMerchantAccountService merchantAccountService) : Controller
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
    public async Task<IActionResult> FillMerchantApplicationDemoData(CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await merchantAccountService.SaveDraftAsync(
                organizationId,
                UserId(),
                DemoMerchantApplication().ToRequest(),
                cancellationToken);
            TempData["MerchantStatus"] = "Merchant application demo data saved.";
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
        var value = User.FindFirstValue("organization_id") ?? Request.Query["organizationId"].FirstOrDefault();
        return Guid.TryParse(value, out organizationId);
    }

    private Guid OrganizationId()
    {
        return TryOrganizationId(out var organizationId)
            ? organizationId
            : throw new UnauthorizedAccessException("An organization context is required.");
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

    private static MerchantApplicationForm DemoMerchantApplication()
    {
        return new MerchantApplicationForm
        {
            BusinessName = "DEV Smoke Powersports",
            Dba = "DEV Smoke Powersports",
            Ein = "82-1234567",
            TaxId = "821234567",
            BusinessType = "LLC",
            PhysicalAddressLine1 = "100 Main St",
            PhysicalAddressLine2 = "Suite 100",
            PhysicalCity = "Dallas",
            PhysicalRegion = "TX",
            PhysicalPostalCode = "75001",
            PhysicalCountry = "US",
            MailingAddressLine1 = "100 Main St",
            MailingAddressLine2 = "Suite 100",
            MailingCity = "Dallas",
            MailingRegion = "TX",
            MailingPostalCode = "75001",
            MailingCountry = "US",
            OwnerName = "Bradley Molen",
            OwnerEmail = "brad.molen+dev-smoke@example.com",
            OwnerPhone = "2145551212",
            BankName = "First National Test Bank",
            BankRoutingNumber = "111000025",
            BankAccountNumber = "1234567890",
            ExpectedMonthlyVolume = 5000,
            AverageTicket = 100,
            Website = "https://dev-smoke-powersports.example.com",
            Mcc = "5533"
        };
    }
}

public sealed class MerchantApplicationForm
{
    public string BusinessName { get; set; } = string.Empty;

    public string? Dba { get; set; }

    public string? Ein { get; set; }

    public string? TaxId { get; set; }

    public string? BusinessType { get; set; }

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

    public string? BankName { get; set; }

    public string? BankRoutingNumber { get; set; }

    public string? BankAccountNumber { get; set; }

    public decimal? ExpectedMonthlyVolume { get; set; }

    public decimal? AverageTicket { get; set; }

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
            BankName,
            BankRoutingNumber,
            BankAccountNumber,
            ExpectedMonthlyVolume,
            AverageTicket,
            Website,
            Mcc);
    }
}
