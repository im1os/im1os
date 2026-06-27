using System.Security.Claims;
using iM1os.Application.Platform;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

public sealed class PlatformController(
    IPlatformAuthenticationService platformAuthenticationService,
    ITenantManagerService tenantManagerService,
    ITenantProvisioningService tenantProvisioningService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        return View(new PlatformLoginRequest("admin@im1os.com", string.Empty));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(PlatformLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await platformAuthenticationService.LoginAsync(request, cancellationToken);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid platform credentials.");
            return View(request);
        }

        var claims = new List<Claim>
        {
            new("platform_user_id", result.UserId.ToString()),
            new(ClaimTypes.Email, result.Email),
            new(ClaimTypes.Name, result.DisplayName),
            new(ClaimTypes.Role, result.Role)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return RedirectToAction(nameof(Dashboard));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        return View(await tenantManagerService.GetDashboardAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Tenants(string? query, string? status, CancellationToken cancellationToken)
    {
        ViewData["Query"] = query;
        ViewData["Status"] = status;
        return View(await tenantManagerService.SearchTenantsAsync(query, status, cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public IActionResult CreateTenant()
    {
        return View(DefaultProvisionRequest());
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        try
        {
            var result = await tenantProvisioningService.ProvisionAsync(request, PlatformUserId(), cancellationToken);
            return RedirectToAction(nameof(Provisioned), new { organizationId = result.OrganizationId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(request);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Provisioned(Guid organizationId, CancellationToken cancellationToken)
    {
        var detail = await tenantManagerService.GetTenantDetailAsync(organizationId, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Tenant(Guid organizationId, CancellationToken cancellationToken)
    {
        var detail = await tenantManagerService.GetTenantDetailAsync(organizationId, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    private string? PlatformUserId()
    {
        return User.FindFirstValue("platform_user_id");
    }

    private static ProvisionTenantRequest DefaultProvisionRequest()
    {
        return new ProvisionTenantRequest(
            BusinessName: string.Empty,
            BusinessEmail: string.Empty,
            OwnerName: string.Empty,
            OwnerEmail: string.Empty,
            Phone: string.Empty,
            AddressLine1: string.Empty,
            AddressLine2: null,
            City: string.Empty,
            Region: string.Empty,
            PostalCode: string.Empty,
            Country: "US",
            TimeZone: "America/Chicago",
            SubscriptionPlan: "Starter",
            IsTrial: true,
            DefaultModules: ["Service", "Parts"],
            DefaultLanguage: "en-US",
            DefaultCurrency: "USD");
    }
}
