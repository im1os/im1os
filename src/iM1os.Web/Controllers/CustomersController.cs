using System.Security.Claims;
using iM1os.Application.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class CustomersController(ICustomerCrmService customerService) : Controller
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
        var customerId = await customerService.CreateCustomerAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToAction(nameof(Detail), new { customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateCustomerRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.UpdateCustomerAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(AddCustomerNoteRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddNoteAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
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
    public async Task<IActionResult> AddDocument(AddCustomerDocumentRequest request, string? returnTab, CancellationToken cancellationToken)
    {
        await customerService.AddDocumentAsync(OrganizationId(), UserId(), request, RemoteIp(), cancellationToken);
        return RedirectToDetail(request.CustomerId, returnTab);
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
