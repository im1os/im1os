using System.Security.Claims;
using iM1os.Application.Customers;
using iM1os.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class CustomersController(ICustomerCrmService customerService, IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? status, CancellationToken cancellationToken)
    {
        var workspace = await customerService.GetWorkspaceAsync(OrganizationId(), new CustomerSearchRequest(query, status), cancellationToken);
        return View(workspace);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid customerId, CancellationToken cancellationToken)
    {
        var detail = await customerService.GetDetailAsync(OrganizationId(), customerId, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpGet]
    public IActionResult New()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        Guid customerId;
        try
        {
            customerId = await customerService.CreateCustomerAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["CustomerError"] = ex.Message;
            return RedirectToAction(nameof(New));
        }

        return RedirectToAction(nameof(Detail), new { customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateCustomerRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await customerService.UpdateCustomerAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["CustomerError"] = ex.Message;
            return RedirectToDetail(request.CustomerId, returnTab);
        }

        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(AddCustomerNoteRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await customerService.AddNoteAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["CustomerError"] = ex.Message;
        }

        return RedirectToDetail(request.CustomerId, returnTab ?? "notes");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(AddCustomerAddressRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddAddressAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPhone(AddCustomerPhoneRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddPhoneAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUnit(AddCustomerUnitRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await customerService.AddUnitAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["CustomerError"] = ex.Message;
        }

        return RedirectToDetail(request.CustomerId, returnTab ?? "units");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUnitAttachment(AddCustomerUnitAttachmentRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            await customerService.AddUnitAttachmentAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["CustomerError"] = ex.Message;
        }

        return RedirectToDetail(request.CustomerId, returnTab ?? "units");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTag(AddCustomerTagRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddTagAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomField(AddCustomerCustomFieldRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddCustomFieldAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExternalLink(AddCustomerExternalLinkRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddExternalLinkAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDocument(AddCustomerDocumentRequest request, IReadOnlyCollection<IFormFile>? files, string? returnTab, CancellationToken cancellationToken)
    {
        try
        {
            var organizationId = OrganizationId();
            var uploads = await DocumentUploadStorage.SaveAsync(environment, organizationId, "customers", request.CustomerId, files, cancellationToken);
            if (uploads.Count == 0)
            {
                await customerService.AddDocumentAsync(organizationId, UserId(), request, RemoteIp(), cancellationToken);
            }
            else
            {
                foreach (var upload in uploads)
                {
                    await customerService.AddDocumentAsync(
                        organizationId,
                        UserId(),
                        request with
                        {
                            FileName = upload.FileName,
                            Url = upload.Url,
                            ContentType = upload.ContentType,
                            StorageKey = upload.StorageKey
                        },
                        RemoteIp(),
                        cancellationToken);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["CustomerError"] = ex.Message;
        }

        return RedirectToDetail(request.CustomerId, returnTab ?? "documents");
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

    private IActionResult RedirectToDetail(Guid customerId, string? returnTab)
    {
        var url = Url.Action(nameof(Detail), new { customerId }) ?? $"/company/customers/{customerId}";
        return Redirect(string.IsNullOrWhiteSpace(returnTab) ? url : $"{url}#{Uri.EscapeDataString(returnTab)}");
    }

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
