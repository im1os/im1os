using System.Security.Claims;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.WorkOrders;
using iM1os.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class WorkOrdersController(
    IWorkOrderService workOrderService,
    ISupplierItemSearchService supplierItemSearchService,
    IWebHostEnvironment environment) : Controller
{
    private const int WorkOrderItemSearchPageSize = 12;

    [HttpGet]
    public async Task<IActionResult> Intake(CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        return View(await workOrderService.GetIntakeAsync(organizationId, VerifiedIntakeEmployeeId(organizationId), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyIntakePin(string? pin, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        var result = await workOrderService.VerifyIntakePinAsync(organizationId, pin, cancellationToken);
        if (result is null)
        {
            TempData["WorkOrderError"] = "Enter a valid employee PIN to open intake.";
            return RedirectToAction(nameof(Intake));
        }

        HttpContext.Session.SetString(IntakeEmployeeSessionKey(organizationId), result.EmployeeId.ToString());
        TempData["WorkOrderStatus"] = $"Unlocked for {result.DisplayName}.";
        return RedirectToAction(nameof(Intake));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearIntakePin()
    {
        var organizationId = OrganizationId();
        HttpContext.Session.Remove(IntakeEmployeeSessionKey(organizationId));
        return RedirectToAction(nameof(Intake));
    }

    [HttpGet]
    public async Task<IActionResult> CustomerLookup(string? query, CancellationToken cancellationToken)
    {
        return Json(await workOrderService.LookupCustomerAsync(OrganizationId(), query, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> YmmTypes(CancellationToken cancellationToken)
    {
        return Json(await workOrderService.GetYmmTypesAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> YmmYears(string? vehicleType, CancellationToken cancellationToken)
    {
        return Json(await workOrderService.GetYmmYearsAsync(vehicleType, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> YmmMakes(string? vehicleType, int year, CancellationToken cancellationToken)
    {
        return Json(await workOrderService.GetYmmMakesAsync(vehicleType, year, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> YmmModels(string? vehicleType, int year, string make, CancellationToken cancellationToken)
    {
        return Json(await workOrderService.GetYmmModelsAsync(vehicleType, year, make, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? stage, CancellationToken cancellationToken)
    {
        var workspace = await workOrderService.GetWorkspaceAsync(OrganizationId(), new WorkOrderSearchRequest(query, stage), cancellationToken);
        return View(workspace);
    }

    [HttpGet]
    public async Task<IActionResult> New(CancellationToken cancellationToken)
    {
        return View("Edit", await workOrderService.GetNewEditorAsync(OrganizationId(), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid workOrderId, CancellationToken cancellationToken)
    {
        var editor = await workOrderService.GetEditorAsync(OrganizationId(), workOrderId, cancellationToken);
        return editor is null ? NotFound() : View(editor);
    }

    [HttpGet]
    public async Task<IActionResult> ItemLookup(string? query, string? supplierCode, string? vehicleType, int? year, string? make, string? model, bool laborOnly, int offset, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (laborOnly)
        {
            var laborItems = await workOrderService.SearchLaborItemsAsync(organizationId, query, WorkOrderItemSearchPageSize, cancellationToken);
            return Json(new
            {
                HasMore = false,
                NextOffset = laborItems.Count,
                Results = laborItems.Select(x => new
                {
                    LaborOperationId = x.Id,
                    SupplierCode = "LABOR",
                    SupplierName = "Labor",
                    SupplierSku = x.Code,
                    ManufacturerPartNumber = (string?)null,
                    Upc = (string?)null,
                    Brand = x.ServiceCategory ?? "Labor",
                    Title = x.Name,
                    Category = x.ServiceCategory,
                    Status = "Active",
                    Msrp = (decimal?)x.Rate,
                    DealerCost = (decimal?)null,
                    ActualCost = (decimal?)null,
                    ImageUrl = (string?)null,
                    LineType = "Labor",
                    Quantity = x.BaseHours ?? 1m,
                    Rate = x.Rate,
                    IsTaxable = x.IsTaxable
                })
            });
        }

        var page = await supplierItemSearchService.SearchForCompanyAsync(
            organizationId,
            new SupplierItemSearchRequest(query, supplierCode, vehicleType, year, make, model, offset, SearchExecuted: true),
            WorkOrderItemSearchPageSize,
            cancellationToken);

        return Json(new
        {
            page.HasMore,
            NextOffset = page.Offset + page.Results.Count,
            Results = page.Results.Select(x => new
            {
                x.SupplierProductId,
                x.SupplierCode,
                x.SupplierName,
                x.SupplierSku,
                x.ManufacturerPartNumber,
                x.Upc,
                x.Brand,
                x.Title,
                x.Category,
                x.Status,
                x.Msrp,
                x.DealerCost,
                x.ActualCost,
                x.ImageUrl,
                LineType = "Parts",
                Quantity = 1m,
                Rate = x.Msrp ?? x.ActualCost ?? x.DealerCost ?? 0m,
                IsTaxable = true
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> FitmentItemCount(string? vehicleType, int year, string make, string model, CancellationToken cancellationToken)
    {
        return Json(new
        {
            Count = await supplierItemSearchService.CountFitmentItemsForCompanyAsync(OrganizationId(), vehicleType, year, make, model, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIntake(
        CreateWorkOrderIntakeRequest request,
        IReadOnlyCollection<IFormFile>? mediaFiles,
        IReadOnlyCollection<IFormFile>? photoFiles,
        IReadOnlyCollection<IFormFile>? videoFiles,
        CancellationToken cancellationToken)
    {
        WorkOrderIntakeResult result;
        try
        {
            var verifiedEmployeeId = VerifiedIntakeEmployeeId(OrganizationId());
            if (verifiedEmployeeId is null)
            {
                throw new InvalidOperationException("Enter an employee PIN before creating an intake work order.");
            }

            request.ServiceAdvisorEmployeeId = verifiedEmployeeId;
            result = await workOrderService.CreateFromIntakeAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
            var uploads = new List<WorkOrderAttachmentUpload>();
            uploads.AddRange(await SaveUploadsAsync(result.WorkOrderId, "Media", mediaFiles, cancellationToken));
            uploads.AddRange(await SaveUploadsAsync(result.WorkOrderId, "Photo", photoFiles, cancellationToken));
            uploads.AddRange(await SaveUploadsAsync(result.WorkOrderId, "Video", videoFiles, cancellationToken));

            if (uploads.Count > 0)
            {
                await workOrderService.AddAttachmentsAsync(
                    OrganizationId(),
                    UserId(),
                    new AddWorkOrderAttachmentRequest { WorkOrderId = result.WorkOrderId, Attachments = uploads },
                    RemoteIp(),
                    cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkOrderError"] = ex.Message;
            return RedirectToAction(nameof(Intake));
        }

        return RedirectToAction(nameof(Edit), new { workOrderId = result.WorkOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaveWorkOrderRequest request, CancellationToken cancellationToken)
    {
        Guid workOrderId;
        try
        {
            workOrderId = await workOrderService.SaveAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkOrderError"] = ex.Message;
            if (request.WorkOrderId is Guid existingWorkOrderId)
            {
                return RedirectToAction(nameof(Edit), new { workOrderId = existingWorkOrderId });
            }

            return RedirectToAction(nameof(New));
        }

        return RedirectToAction(nameof(Edit), new { workOrderId });
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

    private Guid? VerifiedIntakeEmployeeId(Guid organizationId)
    {
        var employeeId = HttpContext.Session.GetString(IntakeEmployeeSessionKey(organizationId));
        return Guid.TryParse(employeeId, out var parsed) ? parsed : null;
    }

    private static string IntakeEmployeeSessionKey(Guid organizationId) => $"workorders:intake:employee:{organizationId:N}";

    private async Task<IReadOnlyCollection<WorkOrderAttachmentUpload>> SaveUploadsAsync(Guid workOrderId, string attachmentType, IReadOnlyCollection<IFormFile>? files, CancellationToken cancellationToken)
    {
        var stored = await DocumentUploadStorage.SaveAsync(environment, OrganizationId(), "work-orders", workOrderId, files, cancellationToken);
        return stored
            .Select(x => new WorkOrderAttachmentUpload(attachmentType, x.FileName, x.Url, x.ContentType))
            .ToList();
    }
}
