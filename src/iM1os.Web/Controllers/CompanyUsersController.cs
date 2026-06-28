using System.Security.Claims;
using iM1os.Application.CompanyUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class CompanyUsersController(ICompanyUserService companyUserService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? status, string? role, Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await companyUserService.GetWorkspaceAsync(
                OrganizationId(),
                UserId(),
                new CompanyUserSearchRequest(query, status, role, userId),
                cancellationToken);
            return View(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCompanyUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await companyUserService.CreateUserAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToAction(nameof(Index), new { userId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateCompanyUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await companyUserService.UpdateUserAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToAction(nameof(Index), new { userId = request.UserId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(SaveCompanyUserPermissionOverridesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await companyUserService.SavePermissionOverridesAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToAction(nameof(Index), new { userId = request.UserId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(CompanyUserSecurityActionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await companyUserService.RunSecurityActionAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToAction(nameof(Index), new { userId = request.UserId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    private Guid OrganizationId()
    {
        var organizationId = User.FindFirstValue("organization_id") ?? Request.Query["organizationId"].FirstOrDefault();
        return Guid.TryParse(organizationId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A company context is required.");
    }

    private Guid UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("platform_user_id");
        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A user context is required.");
    }

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
