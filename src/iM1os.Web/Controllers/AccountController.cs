using System.Security.Claims;
using iM1os.Application.TenantIdentity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

public sealed class AccountController(ITenantIdentityService tenantIdentityService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new TenantLoginRequest(string.Empty, string.Empty, null, false));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(TenantLoginRequest request, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await tenantIdentityService.LoginAsync(request, RemoteIp(), cancellationToken);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(request);
        }

        await SignInTenantAsync(result, request.RememberMe);
        if (result.RequiresOnboarding)
        {
            return RedirectToAction("Index", "Onboarding");
        }

        return LocalRedirect(SafeReturnUrl(returnUrl) ?? Url.Action("Dashboard", "Business")!);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Activate(string token)
    {
        return View(new ActivateOwnerRequest(token, string.Empty, string.Empty));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(ActivateOwnerRequest request, CancellationToken cancellationToken)
    {
        var result = await tenantIdentityService.ActivateOwnerAsync(request, RemoteIp(), cancellationToken);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "The activation link is invalid or expired, or the password does not meet requirements.");
            return View(request);
        }

        await SignInTenantAsync(result, isPersistent: false);
        return result.RequiresOnboarding
            ? RedirectToAction("Index", "Onboarding")
            : RedirectToAction("Dashboard", "Business");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new PasswordResetRequestDto(string.Empty, null));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(PasswordResetRequestDto request, CancellationToken cancellationToken)
    {
        await tenantIdentityService.RequestPasswordResetAsync(request, RemoteIp(), cancellationToken);
        ViewData["ResetRequested"] = true;
        return View(request);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string token)
    {
        return View(new CompletePasswordResetRequest(token, string.Empty, string.Empty));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(CompletePasswordResetRequest request, CancellationToken cancellationToken)
    {
        var completed = await tenantIdentityService.CompletePasswordResetAsync(request, RemoteIp(), cancellationToken);
        if (!completed)
        {
            ModelState.AddModelError(string.Empty, "The reset link is invalid or expired, or the password does not meet requirements.");
            return View(request);
        }

        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            Guid.TryParse(User.FindFirstValue("organization_id"), out var organizationId))
        {
            await tenantIdentityService.LogoutAsync(organizationId, userId, RemoteIp(), cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInTenantAsync(TenantLoginResult result, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Email, result.Email),
            new(ClaimTypes.Name, result.DisplayName),
            new("organization_id", result.OrganizationId.ToString()),
            new("organization_name", result.OrganizationName),
            new("auth_context", "tenant")
        };
        claims.AddRange(result.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(result.Permissions.Select(permission => new Claim("permission", permission)));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = isPersistent });
    }

    private string? RemoteIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? SafeReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    }
}
