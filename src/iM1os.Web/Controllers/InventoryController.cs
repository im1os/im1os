using System.Security.Claims;
using iM1os.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class InventoryController(ICompanyInventoryService inventoryService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query, Guid? locationId, bool lowStockOnly, bool stockedOnly, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        ViewBag.OrganizationId = organizationId;
        return View(await inventoryService.GetWorkspaceAsync(
            organizationId,
            new CompanyInventorySearchRequest(query, locationId, lowStockOnly, stockedOnly),
            cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Add(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        ViewBag.OrganizationId = organizationId;
        return View(await inventoryService.GetAddPageAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Scanner(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        ViewBag.OrganizationId = organizationId;
        return View(await inventoryService.GetAddPageAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SupplierLookup(string? lookupValue, string? supplierCode, Guid? supplierProductId, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return Unauthorized();
        }

        return Json(await inventoryService.LookupSupplierItemAsync(
            organizationId,
            new CompanyInventorySupplierLookupRequest(supplierProductId, lookupValue, supplierCode),
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await inventoryService.CreateCustomItemAsync(organizationId, UserId(), request, cancellationToken);
            TempData["InventoryStatus"] = "Inventory item created.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["InventoryError"] = ex.Message;
        }

        return RedirectToAction(nameof(Add));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplierItem(CompanyInventorySupplierItemRequest request, string? returnTo, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await inventoryService.AddSupplierItemAsync(organizationId, UserId(), request, cancellationToken);
            TempData["InventoryStatus"] = "Supplier item added to inventory.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["InventoryError"] = ex.Message;
        }

        return string.Equals(returnTo, "scanner", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Scanner), new { organizationId })
            : RedirectToAction(nameof(Index), new { organizationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockPolicy(CompanyInventoryLocationStockRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await inventoryService.SaveLocationStockAsync(organizationId, UserId(), request, cancellationToken);
            TempData["InventoryStatus"] = "Location stock settings saved.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["InventoryError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(CompanyInventoryStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await inventoryService.AdjustStockAsync(organizationId, UserId(), request, cancellationToken);
            TempData["InventoryStatus"] = "Inventory adjustment saved.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["InventoryError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCsv(IFormFile? file, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (file is null || file.Length == 0)
        {
            TempData["InventoryError"] = "Choose a CSV file to import.";
            return RedirectToAction(nameof(Add));
        }

        await using var stream = file.OpenReadStream();
        var result = await inventoryService.ImportCsvAsync(organizationId, UserId(), stream, cancellationToken);
        TempData["InventoryStatus"] = $"Inventory import processed {result.Processed:N0} rows, created {result.Created:N0}, updated {result.Updated:N0}, failed {result.Failed:N0}.";
        if (result.Errors.Count > 0)
        {
            TempData["InventoryError"] = string.Join(" ", result.Errors.Take(3));
        }

        return RedirectToAction(nameof(Add));
    }

    private Guid OrganizationId()
    {
        return TryOrganizationId(out var organizationId)
            ? organizationId
            : throw new UnauthorizedAccessException("An organization context is required.");
    }

    private bool TryOrganizationId(out Guid organizationId)
    {
        var value = User.FindFirstValue("organization_id")
            ?? Request.Query["organizationId"].FirstOrDefault()
            ?? (Request.HasFormContentType ? Request.Form["organizationId"].FirstOrDefault() : null);

        return Guid.TryParse(value, out organizationId);
    }

    private Guid UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("platform_user_id");

        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException("A user context is required.");
    }

    private IActionResult RedirectToMissingOrganizationContext()
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction("Tenants", "Platform")
            : RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }
}
