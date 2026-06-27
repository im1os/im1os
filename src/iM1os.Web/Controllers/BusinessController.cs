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
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        ViewBag.OrganizationId = OrganizationId();
        return View(await onboardingService.GetDashboardAsync(OrganizationId(), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Administration(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await businessAdministrationService.GetWorkspaceAsync(OrganizationId(), UserId(), cancellationToken);
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
        if (!await RunOwnerActionAsync(() => businessAdministrationService.UpdateBusinessProfileAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAdministration();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.UpsertLocationAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAdministration();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Employee(InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.InviteEmployeeAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAdministration();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Labor(LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveLaborConfigurationAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAdministration();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Taxes(TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveTaxConfigurationAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAdministration();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveNotificationPreferencesAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAdministration();
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

    private Guid UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("platform_user_id");

        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A user context is required.");
    }

    private IActionResult RedirectToAdministration()
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction(nameof(Administration), new { organizationId = OrganizationId() })
            : RedirectToAction(nameof(Administration));
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
