using System.Security.Claims;
using iM1os.Application.TenantIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class ProfileController(ITenantProfileService profileService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await profileService.GetAsync(OrganizationId(), UserId(), cancellationToken);
        return profile is null ? NotFound() : View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(UpdateTenantProfileRequest request, CancellationToken cancellationToken)
    {
        await profileService.UpdateAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var changed = await profileService.ChangePasswordAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        if (!changed)
        {
            TempData["PasswordError"] = "Password change failed.";
        }

        return RedirectToAction(nameof(Index));
    }

    private Guid OrganizationId() => Guid.Parse(User.FindFirstValue("organization_id")!);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
