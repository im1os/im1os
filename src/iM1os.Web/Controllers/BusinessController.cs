using System.Security.Claims;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.TenantIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class BusinessController(
    IBusinessOnboardingService onboardingService,
    IBusinessAdministrationService businessAdministrationService) : Controller
{
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        ViewBag.OrganizationId = organizationId;
        return View(await onboardingService.GetDashboardAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Administration(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        try
        {
            var workspace = await businessAdministrationService.GetWorkspaceAsync(organizationId, UserId(), cancellationToken);
            return View(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            if (!await RunOwnerActionAsync(() => businessAdministrationService.UpdateBusinessProfileAsync(organizationId, UserId(), request, cancellationToken)))
            {
                return View("AccessDenied");
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanyAdminError"] = ex.Message;
            return RedirectToAdministration(organizationId);
        }

        TempData["CompanyAdminStatus"] = "Company profile saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.UpsertLocationAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Company location saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Employee(InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.InviteEmployeeAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Employee invitation created.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Labor(LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveLaborConfigurationAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Labor configuration saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Taxes(TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveTaxConfigurationAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Tax configuration saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveNotificationPreferencesAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Notification preferences saved.";
        return RedirectToAdministration(organizationId);
    }

    private Guid OrganizationId()
    {
        var organizationId = User.FindFirstValue("organization_id")
            ?? Request.Query["organizationId"].FirstOrDefault()
            ?? (Request.HasFormContentType ? Request.Form["organizationId"].FirstOrDefault() : null);

        return Guid.TryParse(organizationId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("An organization context is required.");
    }

    private bool TryOrganizationId(out Guid organizationId)
    {
        var value = User.FindFirstValue("organization_id")
            ?? Request.Query["organizationId"].FirstOrDefault()
            ?? (Request.HasFormContentType ? Request.Form["organizationId"].FirstOrDefault() : null);

        return Guid.TryParse(value, out organizationId);
    }

    private Guid UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("platform_user_id");

        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A user context is required.");
    }

    private IActionResult RedirectToAdministration(Guid organizationId)
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction(nameof(Administration), new { organizationId })
            : RedirectToAction(nameof(Administration));
    }

    private IActionResult RedirectToMissingOrganizationContext()
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction("Tenants", "Platform")
            : RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }

    private static async Task<bool> RunOwnerActionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
