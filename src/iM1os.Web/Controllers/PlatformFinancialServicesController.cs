using System.Security.Claims;
using iM1os.Application.FinancialServices;
using iM1os.Application.FinancialServices.Merchant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class PlatformFinancialServicesController(IMerchantAccountService merchantAccountService) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(PlatformFinancialServicesModuleCatalog.Workspace());
    }

    [HttpGet]
    public IActionResult Module(string moduleKey)
    {
        return ModuleFor(moduleKey);
    }

    [HttpGet]
    public async Task<IActionResult> MerchantApplications(CancellationToken cancellationToken)
    {
        return View(await merchantAccountService.GetPlatformMerchantApplicationsAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> ActiveMerchants(CancellationToken cancellationToken)
    {
        return View(await merchantAccountService.GetPlatformActiveMerchantsAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> MerchantApplication(Guid organizationId, Guid merchantAccountId, CancellationToken cancellationToken)
    {
        try
        {
            return View(await merchantAccountService.GetPlatformMerchantApplicationAsync(
                organizationId,
                merchantAccountId,
                cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveMerchant(Guid organizationId, Guid merchantAccountId, CancellationToken cancellationToken)
    {
        try
        {
            await merchantAccountService.ApproveApplicationAsync(organizationId, merchantAccountId, UserId(), cancellationToken);
            TempData["MerchantStatus"] = "Merchant approved, NMI merchant created, and payments enabled.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            TempData["MerchantError"] = ex.Message;
        }

        return RedirectToAction(nameof(MerchantApplications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshMerchantStatus(Guid organizationId, Guid merchantAccountId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await merchantAccountService.RefreshProviderStatusAsync(
                organizationId,
                merchantAccountId,
                UserId(),
                cancellationToken);
            TempData["MerchantStatus"] = result.Status == "Active"
                ? "NMI merchant approved and payments enabled."
                : $"NMI merchant status refreshed: {result.Status}.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            TempData["MerchantError"] = ex.Message;
        }

        return RedirectToAction(nameof(MerchantApplications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMerchant(Guid organizationId, Guid merchantAccountId, string? reason, CancellationToken cancellationToken)
    {
        await merchantAccountService.RejectApplicationAsync(organizationId, merchantAccountId, UserId(), reason, cancellationToken);
        TempData["MerchantStatus"] = "Merchant application rejected.";
        return RedirectToAction(nameof(MerchantApplications));
    }
    [HttpGet] public IActionResult UnderwritingQueue() => ModuleFor("underwriting-queue");
    [HttpGet] public IActionResult RiskMonitoring() => ModuleFor("risk-monitoring");
    [HttpGet] public IActionResult ProcessorManagement() => ModuleFor("processor-management");
    [HttpGet] public IActionResult GatewayProviders() => ModuleFor("gateway-providers");
    [HttpGet] public IActionResult ResidualReporting() => ModuleFor("residual-reporting");
    [HttpGet] public IActionResult SettlementMonitoring() => ModuleFor("settlement-monitoring");
    [HttpGet] public IActionResult ChargebackManagement() => ModuleFor("chargeback-management");
    [HttpGet] public IActionResult HardwareCatalog() => ModuleFor("hardware-catalog");
    [HttpGet] public IActionResult DeviceInventory() => ModuleFor("device-inventory");
    [HttpGet] public IActionResult ShippingFulfillment() => ModuleFor("shipping-fulfillment");
    [HttpGet] public IActionResult PricingPlans() => ModuleFor("pricing-plans");
    [HttpGet] public IActionResult MerchantSupport() => ModuleFor("merchant-support");
    [HttpGet] public IActionResult ProviderConfiguration() => ModuleFor("provider-configuration");

    private IActionResult ModuleFor(string moduleKey)
    {
        var module = PlatformFinancialServicesModuleCatalog.Find(moduleKey);
        return module is null ? NotFound() : View("Module", module);
    }

    private Guid UserId()
    {
        var userId = User.FindFirstValue("platform_user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A platform user context is required.");
    }
}
