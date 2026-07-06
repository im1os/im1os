using System.Security.Claims;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.Employees;
using iM1os.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
[Route("company/hr")]
public sealed class HrController(
    IBusinessAdministrationService businessAdministrationService,
    IEmployeeService employeeService) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Employees));

    [HttpGet("employees")]
    public IActionResult Employees() => RedirectToAction("Index", "Employees");

    [HttpGet("time-clock")]
    public async Task<IActionResult> TimeClock(CancellationToken cancellationToken) =>
        View(await PageAsync("Time Clock", "Clock employees in and out, manage manual punches, and review recent time activity.", cancellationToken));

    [HttpGet("work-schedule")]
    public async Task<IActionResult> WorkSchedule(CancellationToken cancellationToken) =>
        View(await PageAsync("Work Schedule", "Manage upcoming employee shifts and scheduled labor coverage.", cancellationToken));

    [HttpGet("time-off")]
    public async Task<IActionResult> TimeOff(CancellationToken cancellationToken) =>
        View(await PageAsync("Time Off", "Track time off requests, approval status, and paid hours.", cancellationToken));

    [HttpGet("payroll")]
    public async Task<IActionResult> Payroll(CancellationToken cancellationToken) =>
        View(await PageAsync("Payroll", "Review payroll period totals, paid hours, variance, and gross pay.", cancellationToken));

    [HttpGet("sales-commissions")]
    public async Task<IActionResult> SalesCommissions(CancellationToken cancellationToken) =>
        View(await PageAsync("Sales Commissions", "Review sales commission setup from employee compensation records.", cancellationToken));

    [HttpGet("work-order-commissions")]
    public async Task<IActionResult> WorkOrderCommissions(CancellationToken cancellationToken) =>
        View(await PageAsync("Work Order Commissions", "Review work order commission setup from employee compensation records.", cancellationToken));

    [HttpGet("certifications")]
    public async Task<IActionResult> Certifications(CancellationToken cancellationToken) =>
        View(await PageAsync("Certifications", "Track certification documents from employee records.", cancellationToken));

    [HttpGet("documents")]
    public async Task<IActionResult> Documents(CancellationToken cancellationToken) =>
        View(await PageAsync("Documents", "Review employee HR, payroll, certification, and safety documents.", cancellationToken));

    [HttpGet("safety")]
    public async Task<IActionResult> Safety(CancellationToken cancellationToken) =>
        View(await PageAsync("OSHA / Safety", "Log safety incidents, OSHA recordable events, reports, and lost-time totals.", cancellationToken));

    [HttpGet("company-assets")]
    public async Task<IActionResult> CompanyAssets(CancellationToken cancellationToken) =>
        View(await PageAsync("Company Assets", "Assign and recover tools, keys, devices, and other company property.", cancellationToken));

    [HttpGet("performance-reviews")]
    public async Task<IActionResult> PerformanceReviews(CancellationToken cancellationToken) =>
        View(await PageAsync("Performance Reviews", "Review employee performance status and follow-up needs.", cancellationToken));

    [HttpPost("time-clock/punch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TimeClockPunch(ClockEmployeeRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.ClockEmployeeAsync(OrganizationId(), UserId(), request, cancellationToken),
            request.Action.Equals("out", StringComparison.OrdinalIgnoreCase) ? "Employee clocked out." : "Employee clocked in.");
        return RedirectToAction(nameof(TimeClock));
    }

    [HttpPost("time-clock/manual-punch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPunch(AddTimePunchRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.AddTimePunchAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Manual time punch added.");
        return RedirectToAction(nameof(TimeClock));
    }

    [HttpPost("time-clock/update-punch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePunch(UpdateTimePunchRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.UpdateTimePunchAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Time punch updated.");
        return RedirectToAction(nameof(TimeClock));
    }

    [HttpPost("time-clock/delete-punch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePunch(DeleteTimePunchRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.DeleteTimePunchAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Time punch deleted.");
        return RedirectToAction(nameof(TimeClock));
    }

    [HttpPost("work-schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddScheduleShift(AddScheduleShiftRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.AddScheduleShiftAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Shift scheduled.");
        return RedirectToAction(nameof(WorkSchedule));
    }

    [HttpPost("work-schedule/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteScheduleShift(DeleteScheduleShiftRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.DeleteScheduleShiftAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Shift deleted.");
        return RedirectToAction(nameof(WorkSchedule));
    }

    [HttpPost("time-off")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeOff(AddTimeOffRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.AddTimeOffAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Time off request added.");
        return RedirectToAction(nameof(TimeOff));
    }

    [HttpPost("time-off/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTimeOffStatus(SetTimeOffStatusRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.SetTimeOffStatusAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Time off status updated.");
        return RedirectToAction(nameof(TimeOff));
    }

    [HttpPost("time-off/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTimeOff(DeleteTimeOffRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.DeleteTimeOffAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Time off request deleted.");
        return RedirectToAction(nameof(TimeOff));
    }

    [HttpPost("company-assets")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCompanyAsset(AddCompanyAssetRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.AddCompanyAssetAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Company asset assigned.");
        return RedirectToAction(nameof(CompanyAssets));
    }

    [HttpPost("company-assets/return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnCompanyAsset(ReturnCompanyAssetRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.ReturnCompanyAssetAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Company asset returned.");
        return RedirectToAction(nameof(CompanyAssets));
    }

    [HttpPost("company-assets/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompanyAsset(DeleteCompanyAssetRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.DeleteCompanyAssetAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Company asset deleted.");
        return RedirectToAction(nameof(CompanyAssets));
    }

    [HttpPost("safety")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSafetyIncident(AddSafetyIncidentRequest request, CancellationToken cancellationToken)
    {
        await RunHrActionAsync(
            () => businessAdministrationService.AddSafetyIncidentAsync(OrganizationId(), UserId(), request, cancellationToken),
            "Safety incident logged.");
        return RedirectToAction(nameof(Safety));
    }

    private async Task<HrWorkspacePage> PageAsync(string title, string description, CancellationToken cancellationToken)
    {
        try
        {
            var organizationId = OrganizationId();
            var userId = UserId();
            return new HrWorkspacePage(
                await businessAdministrationService.GetWorkspaceAsync(organizationId, userId, cancellationToken),
                await employeeService.GetWorkspaceAsync(organizationId, userId, new EmployeeSearchRequest(null, null, null, null), cancellationToken),
                title,
                description);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
    }

    private async Task RunHrActionAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            TempData["HrStatus"] = successMessage;
        }
        catch (UnauthorizedAccessException)
        {
            TempData["HrError"] = "HR requires owner or administrator access.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["HrError"] = ex.Message;
        }
    }

    private Guid OrganizationId()
    {
        var organizationId = User.FindFirstValue("organization_id")
            ?? Request.Query["organizationId"].FirstOrDefault()
            ?? (Request.HasFormContentType ? Request.Form["organizationId"].FirstOrDefault() : null);

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
}
