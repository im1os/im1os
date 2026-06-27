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
        return View(await onboardingService.GetDashboardAsync(OrganizationId(), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Administration(CancellationToken cancellationToken)
    {
        try
        {
            return View(await businessAdministrationService.GetWorkspaceAsync(OrganizationId(), UserId(), cancellationToken));
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

        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.UpsertLocationAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Employee(InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.InviteEmployeeAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Labor(LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveLaborConfigurationAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Taxes(TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveTaxConfigurationAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveNotificationPreferencesAsync(OrganizationId(), UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        return RedirectToAction(nameof(Administration));
    }

    private Guid OrganizationId() => Guid.Parse(User.FindFirstValue("organization_id")!);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
