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
        var organizationId = OrganizationId();
        ViewBag.AdminWorkspace = await businessAdministrationService.GetWorkspaceAsync(organizationId, UserId(), cancellationToken);
        return View(await onboardingService.GetDashboardAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Administration(CancellationToken cancellationToken)
    {
        return View(await businessAdministrationService.GetWorkspaceAsync(OrganizationId(), UserId(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        await businessAdministrationService.UpdateBusinessProfileAsync(OrganizationId(), UserId(), request, cancellationToken);
        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        await businessAdministrationService.UpsertLocationAsync(OrganizationId(), UserId(), request, cancellationToken);
        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Employee(InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        await businessAdministrationService.InviteEmployeeAsync(OrganizationId(), UserId(), request, cancellationToken);
        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Labor(LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        await businessAdministrationService.SaveLaborConfigurationAsync(OrganizationId(), UserId(), request, cancellationToken);
        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Taxes(TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        await businessAdministrationService.SaveTaxConfigurationAsync(OrganizationId(), UserId(), request, cancellationToken);
        return RedirectToAction(nameof(Administration));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        await businessAdministrationService.SaveNotificationPreferencesAsync(OrganizationId(), UserId(), request, cancellationToken);
        return RedirectToAction(nameof(Administration));
    }

    private Guid OrganizationId() => Guid.Parse(User.FindFirstValue("organization_id")!);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
