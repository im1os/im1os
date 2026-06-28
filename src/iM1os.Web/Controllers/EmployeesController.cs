using System.Security.Claims;
using iM1os.Application.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class EmployeesController(IEmployeeService employeeService) : Controller
{
    private static readonly HashSet<string> EmployeeEditorTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "general",
        "employment",
        "compensation",
        "login-account",
        "permissions",
        "security",
        "activity"
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? status, string? role, Guid? employeeId, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await employeeService.GetWorkspaceAsync(
                OrganizationId(),
                UserId(),
                new EmployeeSearchRequest(query, status, role, employeeId),
                cancellationToken);
            return View(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid employeeId, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await employeeService.GetWorkspaceAsync(
                OrganizationId(),
                UserId(),
                new EmployeeSearchRequest(null, null, null, employeeId),
                cancellationToken);

            if (workspace.SelectedEmployee is null)
            {
                return NotFound();
            }

            return View(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var employeeId = await employeeService.CreateEmployeeAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToAction(nameof(Edit), new { employeeId });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateEmployeeRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.UpdateEmployeeAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableLogin(EnableEmployeeLoginRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.EnableLoginAccountAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compensation(SaveEmployeeCompensationRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.SaveCompensationAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompensation(DeleteEmployeeCompensationRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.DeleteCompensationAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin(SaveEmployeePinRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.SavePinAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(SaveEmployeePermissionOverridesRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.SavePermissionOverridesAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("AccessDenied", "Business");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(EmployeeSecurityActionRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await employeeService.RunSecurityActionAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            return RedirectToEdit(request.EmployeeId, returnTab);
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

    private IActionResult RedirectToEdit(Guid employeeId, string? returnTab)
    {
        var url = Url.Action(nameof(Edit), new { employeeId }) ?? $"/company/employees/edit?employeeId={employeeId}";
        var tab = CleanReturnTab(returnTab);
        return Redirect(tab is null ? url : $"{url}#{Uri.EscapeDataString(tab)}");
    }

    private static string? CleanReturnTab(string? returnTab)
    {
        return !string.IsNullOrWhiteSpace(returnTab) && EmployeeEditorTabs.Contains(returnTab)
            ? returnTab
            : null;
    }
}
