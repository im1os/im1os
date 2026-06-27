using System.Security.Claims;
using iM1os.Application.TenantIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize(Roles = "Owner")]
public sealed class OnboardingController(IBusinessOnboardingService onboardingService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var draft = await onboardingService.GetDraftAsync(OrganizationId(), cancellationToken);
        return draft is null ? NotFound() : View(draft);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BusinessOnboardingRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        await onboardingService.CompleteAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToAction("Dashboard", "Business");
    }

    private Guid OrganizationId() => Guid.Parse(User.FindFirstValue("organization_id")!);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
