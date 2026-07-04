using System.Security.Claims;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.CompanySuppliers;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.Platform;
using iM1os.Application.TenantIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize]
public sealed class BusinessController(
    IBusinessOnboardingService onboardingService,
    IBusinessAdministrationService businessAdministrationService,
    ICompanySupplierService companySupplierService,
    ISupplierItemSearchService supplierItemSearchService,
    IPlatformSupplierConnectorService platformSupplierConnectorService,
    ITenantModuleEntitlementService tenantModuleEntitlements,
    IWpsLiveInventoryService wpsLiveInventoryService,
    ITurn14LiveInventoryService turn14LiveInventoryService,
    IPartsUnlimitedLiveInventoryService partsUnlimitedLiveInventoryService) : Controller
{
    private const int SupplierItemSearchPageSize = 25;

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        ViewBag.OrganizationId = organizationId;
        return View(await onboardingService.GetDashboardAsync(organizationId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Administration(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        try
        {
            var workspace = await businessAdministrationService.GetWorkspaceAsync(organizationId, UserId(), cancellationToken);
            return View(workspace);
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
    }

    [HttpGet]
    public async Task<IActionResult> SupplierItemSearch(string? query, string? supplierCode, string? vehicleType, int? year, string? make, string? model, bool searchExecuted, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

            ViewBag.OrganizationId = organizationId;
        await SetCompanySupplierConnectorCodesAsync(organizationId, cancellationToken);
        return View(await supplierItemSearchService.SearchForCompanyAsync(
            organizationId,
            new SupplierItemSearchRequest(query, supplierCode, vehicleType, year, make, model, SearchExecuted: searchExecuted),
            SupplierItemSearchPageSize,
            cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SupplierItemSearchResults(string? query, string? supplierCode, string? vehicleType, int? year, string? make, string? model, bool searchExecuted, int offset, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return Unauthorized();
        }

        var page = await supplierItemSearchService.SearchForCompanyAsync(
            organizationId,
            new SupplierItemSearchRequest(query, supplierCode, vehicleType, year, make, model, offset, searchExecuted, IncludeFacets: false),
            SupplierItemSearchPageSize,
            cancellationToken);
        return Json(new
        {
            page.TotalResults,
            page.Offset,
            page.PageSize,
            page.HasMore,
            NextOffset = page.TotalResults,
            Results = page.Results.Select(x => new
            {
                x.SupplierProductId,
                x.GlobalProductId,
                x.SupplierCode,
                x.SupplierName,
                x.SupplierSku,
                x.ManufacturerPartNumber,
                x.Upc,
                x.Brand,
                x.Title,
                x.Category,
                x.Status,
                x.FitmentRecordCount,
                x.Msrp,
                x.DealerCost,
                x.ActualCost,
                x.ImageUrl,
                x.CrossReferences,
                x.IsCrossReference,
                x.Fitment,
                Offers = (x.Offers ?? []).Select(offer => new
                {
                    offer.SupplierProductId,
                    offer.GlobalProductId,
                    offer.SupplierCode,
                    offer.SupplierName,
                    offer.SupplierSku,
                    offer.ManufacturerPartNumber,
                    offer.Upc,
                    offer.Brand,
                    offer.Title,
                    offer.Category,
                    offer.Status,
                    offer.FitmentRecordCount,
                    offer.Msrp,
                    offer.DealerCost,
                    offer.ActualCost,
                    offer.ImageUrl,
                    offer.HasCachedInventory,
                    offer.CachedInventoryTotal,
                    offer.IsDefaultOffer,
                    FetchFitmentUrl = Url.Action(nameof(SupplierFetchItemFitment), new { offer.SupplierProductId, organizationId }),
                    InventoryUrl = offer.SupplierCode == "WPS"
                        ? Url.Action(nameof(SupplierWpsInventory), new { offer.SupplierProductId, organizationId })
                        : offer.SupplierCode == "TURN14"
                            ? Url.Action(nameof(SupplierTurn14Inventory), new { offer.SupplierProductId, organizationId })
                        : null,
                    InventoryBatchUrl = offer.SupplierCode == "PU"
                        ? Url.Action(nameof(SupplierPartsUnlimitedInventory), new { organizationId })
                        : null,
                    InventoryLabel = offer.SupplierCode switch
                    {
                        "WPS" => "WPS Warehouse Inventory",
                        "TURN14" => "Turn14 Warehouse Inventory",
                        "PU" => "Parts Unlimited Warehouse Inventory",
                        _ => null
                    }
                }),
                FetchFitmentUrl = Url.Action(nameof(SupplierFetchItemFitment), new { x.SupplierProductId, organizationId }),
                InventoryUrl = x.SupplierCode == "WPS"
                    ? Url.Action(nameof(SupplierWpsInventory), new { x.SupplierProductId, organizationId })
                    : x.SupplierCode == "TURN14"
                        ? Url.Action(nameof(SupplierTurn14Inventory), new { x.SupplierProductId, organizationId })
                    : null,
                InventoryBatchUrl = x.SupplierCode == "PU"
                    ? Url.Action(nameof(SupplierPartsUnlimitedInventory), new { organizationId })
                    : null,
                InventoryLabel = x.SupplierCode switch
                {
                    "WPS" => "WPS Warehouse Inventory",
                    "TURN14" => "Turn14 Warehouse Inventory",
                    "PU" => "Parts Unlimited Warehouse Inventory",
                    _ => null
                }
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> SupplierWpsConnector(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        try
        {
            ViewBag.OrganizationId = organizationId;
            await SetCompanySupplierConnectorCodesAsync(organizationId, cancellationToken);
            return View(await companySupplierService.GetWpsConnectorAsync(organizationId, UserId(), cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException)
        {
            return View("AccessDenied");
        }
    }

    [HttpGet]
    public async Task<IActionResult> SupplierPartsUnlimitedConnector(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        try
        {
            ViewBag.OrganizationId = organizationId;
            await SetCompanySupplierConnectorCodesAsync(organizationId, cancellationToken);
            return View("SupplierConnector", await companySupplierService.GetPartsUnlimitedConnectorAsync(organizationId, UserId(), cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException)
        {
            return View("AccessDenied");
        }
    }

    [HttpGet]
    public async Task<IActionResult> SupplierTurn14Connector(CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return RedirectToMissingOrganizationContext();
        }

        try
        {
            ViewBag.OrganizationId = organizationId;
            await SetCompanySupplierConnectorCodesAsync(organizationId, cancellationToken);
            return View("SupplierConnector", await companySupplierService.GetTurn14ConnectorAsync(organizationId, UserId(), cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException)
        {
            return View("AccessDenied");
        }
    }

    [HttpGet]
    public async Task<IActionResult> SupplierWpsInventory(Guid supplierProductId, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return Unauthorized();
        }

        return Json(await wpsLiveInventoryService.GetInventoryForCompanyAsync(organizationId, supplierProductId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SupplierTurn14Inventory(Guid supplierProductId, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return Unauthorized();
        }

        return Json(await turn14LiveInventoryService.GetInventoryForCompanyAsync(organizationId, supplierProductId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SupplierPartsUnlimitedInventory([FromQuery] Guid[] supplierProductIds, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out var organizationId))
        {
            return Unauthorized();
        }

        return Json(await partsUnlimitedLiveInventoryService.GetInventoryForCompanyAsync(organizationId, supplierProductIds.Take(SupplierItemSearchPageSize).ToArray(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SupplierFetchItemFitment(Guid supplierProductId, CancellationToken cancellationToken)
    {
        if (!TryOrganizationId(out _))
        {
            return Unauthorized();
        }

        return Json(await platformSupplierConnectorService.QueueSupplierItemFitmentAsync(supplierProductId, UserId().ToString(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            if (!await RunOwnerActionAsync(() => businessAdministrationService.UpdateBusinessProfileAsync(organizationId, UserId(), request, cancellationToken)))
            {
                return View("AccessDenied");
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanyAdminError"] = ex.Message;
            return RedirectToAdministration(organizationId);
        }

        TempData["CompanyAdminStatus"] = "Company profile saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(UpsertLocationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.UpsertLocationAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Company location saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Employee(InviteEmployeeRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.InviteEmployeeAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Employee invitation created.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Labor(LaborConfigurationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveLaborConfigurationAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Labor configuration saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Taxes(TaxConfigurationRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveTaxConfigurationAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Tax configuration saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        if (!await RunOwnerActionAsync(() => businessAdministrationService.SaveNotificationPreferencesAsync(organizationId, UserId(), request, cancellationToken)))
        {
            return View("AccessDenied");
        }

        TempData["CompanyAdminStatus"] = "Notification preferences saved.";
        return RedirectToAdministration(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplierWpsConnector(CompanyWpsConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await companySupplierService.SaveWpsConnectorAsync(organizationId, UserId(), request, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanySupplierMessage"] = ex.Message;
            return RedirectToSupplierWpsConnector(organizationId);
        }

        TempData["CompanySupplierMessage"] = "Company WPS connector saved.";
        return RedirectToSupplierWpsConnector(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncSupplierWpsDealerPricing(CompanyWpsDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            var page = await companySupplierService.QueueWpsDealerPricingSyncAsync(organizationId, UserId(), request, cancellationToken);
            TempData["CompanySupplierMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanySupplierMessage"] = ex.Message;
        }

        return RedirectToSupplierWpsConnector(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplierPartsUnlimitedConnector(CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await companySupplierService.SavePartsUnlimitedConnectorAsync(organizationId, UserId(), request, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanySupplierMessage"] = ex.Message;
            return RedirectToSupplierPartsUnlimitedConnector(organizationId);
        }

        TempData["CompanySupplierMessage"] = "Company Parts Unlimited connector saved.";
        return RedirectToSupplierPartsUnlimitedConnector(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncSupplierPartsUnlimitedDealerPricing(CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            var page = await companySupplierService.QueuePartsUnlimitedDealerPricingSyncAsync(organizationId, UserId(), request, cancellationToken);
            TempData["CompanySupplierMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanySupplierMessage"] = ex.Message;
        }

        return RedirectToSupplierPartsUnlimitedConnector(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplierTurn14Connector(CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            await companySupplierService.SaveTurn14ConnectorAsync(organizationId, UserId(), request, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanySupplierMessage"] = ex.Message;
            return RedirectToSupplierTurn14Connector(organizationId);
        }

        TempData["CompanySupplierMessage"] = "Company Turn14 connector saved.";
        return RedirectToSupplierTurn14Connector(organizationId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncSupplierTurn14DealerPricing(CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId();
        try
        {
            var page = await companySupplierService.QueueTurn14DealerPricingSyncAsync(organizationId, UserId(), request, cancellationToken);
            TempData["CompanySupplierMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return View("AccessDenied");
        }
        catch (InvalidOperationException ex)
        {
            TempData["CompanySupplierMessage"] = ex.Message;
        }

        return RedirectToSupplierTurn14Connector(organizationId);
    }

    private Guid OrganizationId()
    {
        var organizationId = User.FindFirstValue("organization_id")
            ?? Request.Query["organizationId"].FirstOrDefault()
            ?? (Request.HasFormContentType ? Request.Form["organizationId"].FirstOrDefault() : null);

        return Guid.TryParse(organizationId, out var parsed)
            ? parsed
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

    private IActionResult RedirectToAdministration(Guid organizationId)
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction(nameof(Administration), new { organizationId })
            : RedirectToAction(nameof(Administration));
    }

    private async Task SetCompanySupplierConnectorCodesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        ViewBag.CompanySupplierConnectorCodes = await tenantModuleEntitlements.GetEnabledSupplierConnectorCodesAsync(organizationId, cancellationToken);
    }

    private IActionResult RedirectToSupplierWpsConnector(Guid organizationId)
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction(nameof(SupplierWpsConnector), new { organizationId })
            : RedirectToAction(nameof(SupplierWpsConnector));
    }

    private IActionResult RedirectToSupplierPartsUnlimitedConnector(Guid organizationId)
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction(nameof(SupplierPartsUnlimitedConnector), new { organizationId })
            : RedirectToAction(nameof(SupplierPartsUnlimitedConnector));
    }

    private IActionResult RedirectToSupplierTurn14Connector(Guid organizationId)
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction(nameof(SupplierTurn14Connector), new { organizationId })
            : RedirectToAction(nameof(SupplierTurn14Connector));
    }

    private IActionResult RedirectToMissingOrganizationContext()
    {
        return User.FindFirstValue("platform_user_id") is not null
            ? RedirectToAction("Tenants", "Platform")
            : RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }

    private static async Task<bool> RunOwnerActionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
