using System.Security.Claims;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.Platform;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

public sealed class PlatformController(
    IPlatformAuthenticationService platformAuthenticationService,
    ITenantManagerService tenantManagerService,
    IPlatformSupplierConnectorService platformSupplierConnectorService,
    ISupplierItemSearchService supplierItemSearchService,
    IWpsLiveInventoryService wpsLiveInventoryService,
    ITurn14LiveInventoryService turn14LiveInventoryService,
    IPartsUnlimitedLiveInventoryService partsUnlimitedLiveInventoryService,
    IPlatformOperationsService platformOperationsService,
    ITenantProvisioningService tenantProvisioningService) : Controller
{
    private const int SupplierItemSearchPageSize = 25;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        return View(new PlatformLoginRequest("admin@im1os.com", string.Empty));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(PlatformLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await platformAuthenticationService.LoginAsync(request, cancellationToken);
        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid platform credentials.");
            return View(request);
        }

        var claims = new List<Claim>
        {
            new("platform_user_id", result.UserId.ToString()),
            new(ClaimTypes.Email, result.Email),
            new(ClaimTypes.Name, result.DisplayName),
            new(ClaimTypes.Role, result.Role)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return RedirectToAction(nameof(Dashboard));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        return View(await tenantManagerService.GetDashboardAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Tenants(string? query, string? status, CancellationToken cancellationToken)
    {
        ViewData["Query"] = query;
        ViewData["Status"] = status;
        return View(await tenantManagerService.SearchTenantsAsync(query, status, cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public IActionResult CreateTenant()
    {
        return View(DefaultProvisionRequest());
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        try
        {
            var result = await tenantProvisioningService.ProvisionAsync(request, PlatformUserId(), cancellationToken);
            return RedirectToAction(nameof(Provisioned), new { organizationId = result.OrganizationId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(request);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Provisioned(Guid organizationId, CancellationToken cancellationToken)
    {
        var detail = await tenantManagerService.GetTenantDetailAsync(organizationId, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Tenant(Guid organizationId, CancellationToken cancellationToken)
    {
        var detail = await tenantManagerService.GetTenantDetailAsync(organizationId, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> WpsConnector(CancellationToken cancellationToken)
    {
        return View(await platformSupplierConnectorService.GetWpsConnectorAsync(cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> Turn14Connector(CancellationToken cancellationToken)
    {
        return View(await platformSupplierConnectorService.GetTurn14ConnectorAsync(cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> PartsUnlimitedConnector(CancellationToken cancellationToken)
    {
        return View(await platformSupplierConnectorService.GetPartsUnlimitedConnectorAsync(cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> Scheduler(CancellationToken cancellationToken)
    {
        return View(await platformSupplierConnectorService.GetGlobalSchedulerAsync(cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> Operations(CancellationToken cancellationToken)
    {
        return View(await platformOperationsService.GetOperationsAsync(cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> ItemSearch(string? query, string? supplierCode, string? vehicleType, int? year, string? make, string? model, bool searchExecuted, CancellationToken cancellationToken)
    {
        return View(await supplierItemSearchService.SearchAsync(new SupplierItemSearchRequest(query, supplierCode, vehicleType, year, make, model, SearchExecuted: searchExecuted), SupplierItemSearchPageSize, cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> ItemSearchResults(string? query, string? supplierCode, string? vehicleType, int? year, string? make, string? model, bool searchExecuted, int offset, CancellationToken cancellationToken)
    {
        var page = await supplierItemSearchService.SearchAsync(
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
                    FetchFitmentUrl = Url.Action(nameof(FetchItemFitment), new { offer.SupplierProductId }),
                    InventoryUrl = offer.SupplierCode switch
                    {
                        "WPS" => Url.Action(nameof(WpsInventory), new { offer.SupplierProductId }),
                        "TURN14" => Url.Action(nameof(Turn14Inventory), new { offer.SupplierProductId }),
                        _ => null
                    },
                    InventoryBatchUrl = offer.SupplierCode == "PU"
                        ? Url.Action(nameof(PartsUnlimitedInventory))
                        : null,
                    InventoryLabel = offer.SupplierCode switch
                    {
                        "WPS" => "WPS Warehouse Inventory",
                        "TURN14" => "Turn14 Warehouse Inventory",
                        "PU" => "Parts Unlimited Warehouse Inventory",
                        _ => null
                    }
                }),
                FetchFitmentUrl = Url.Action(nameof(FetchItemFitment), new { x.SupplierProductId }),
                InventoryUrl = x.SupplierCode switch
                {
                    "WPS" => Url.Action(nameof(WpsInventory), new { x.SupplierProductId }),
                    "TURN14" => Url.Action(nameof(Turn14Inventory), new { x.SupplierProductId }),
                    _ => null
                },
                InventoryBatchUrl = x.SupplierCode == "PU"
                    ? Url.Action(nameof(PartsUnlimitedInventory))
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

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> WpsInventory(Guid supplierProductId, CancellationToken cancellationToken)
    {
        return Json(await wpsLiveInventoryService.GetInventoryAsync(supplierProductId, cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> Turn14Inventory(Guid supplierProductId, CancellationToken cancellationToken)
    {
        return Json(await turn14LiveInventoryService.GetInventoryAsync(supplierProductId, cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> PartsUnlimitedInventory([FromQuery] Guid[] supplierProductIds, CancellationToken cancellationToken)
    {
        return Json(await partsUnlimitedLiveInventoryService.GetInventoryAsync(supplierProductIds.Take(SupplierItemSearchPageSize).ToArray(), cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchItemFitment(Guid supplierProductId, CancellationToken cancellationToken)
    {
        return Json(await platformSupplierConnectorService.QueueSupplierItemFitmentAsync(supplierProductId, PlatformUserId(), cancellationToken));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWpsConnector(WpsConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TempData["PlatformMessage"] = "WPS connector settings saved.";
            await platformSupplierConnectorService.SaveWpsConnectorAsync(request, PlatformUserId(), cancellationToken);
        }
        catch (ArgumentException exception)
        {
            TempData["PlatformMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(WpsConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestWpsConnector(CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.TestWpsConnectionAsync(PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.Status.LastConnectionMessage;
        return RedirectToAction(nameof(WpsConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTurn14Connector(Turn14ConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TempData["PlatformMessage"] = "Turn14 connector settings saved.";
            await platformSupplierConnectorService.SaveTurn14ConnectorAsync(request, PlatformUserId(), cancellationToken);
        }
        catch (ArgumentException exception)
        {
            TempData["PlatformMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Turn14Connector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePartsUnlimitedConnector(PartsUnlimitedConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TempData["PlatformMessage"] = "Parts Unlimited connector settings saved.";
            await platformSupplierConnectorService.SavePartsUnlimitedConnectorAsync(request, PlatformUserId(), cancellationToken);
        }
        catch (ArgumentException exception)
        {
            TempData["PlatformMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTurn14Connector(CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.TestTurn14ConnectionAsync(PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.Status.LastConnectionMessage;
        return RedirectToAction(nameof(Turn14Connector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestPartsUnlimitedConnector(CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.TestPartsUnlimitedConnectionAsync(PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.Status.LastConnectionMessage;
        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public IActionResult RefreshPartsUnlimitedBrandCache()
    {
        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPartsUnlimitedBrandCache(CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.RefreshPartsUnlimitedBrandCacheAsync(PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.Status.LastConnectionMessage;
        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportWpsMasterFile(WpsMasterFileImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportWpsMasterFileAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(WpsConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportTurn14MasterFile(Turn14MasterFileImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportTurn14MasterFileAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(Turn14Connector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportPartsUnlimitedMasterFile(PartsUnlimitedMasterFileImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportPartsUnlimitedMasterFileAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportPartsUnlimitedBrandImages(PartsUnlimitedBrandImagesImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportPartsUnlimitedBrandImagesAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportTurn14Fitment(Turn14FitmentImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportTurn14FitmentAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(Turn14Connector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportTurn14MediaEnrichment(Turn14MediaEnrichmentImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportTurn14MediaEnrichmentAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(Turn14Connector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportPartsUnlimitedFitment(PartsUnlimitedFitmentImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportPartsUnlimitedFitmentAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(PartsUnlimitedConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportWpsFitment(WpsFitmentImportRequest request, CancellationToken cancellationToken)
    {
        var page = await platformSupplierConnectorService.ImportWpsFitmentAsync(request, PlatformUserId(), cancellationToken);
        TempData["PlatformMessage"] = page.RecentImportRuns.FirstOrDefault()?.Message;
        return RedirectToAction(nameof(WpsConnector));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpGet]
    public async Task<IActionResult> EditTenant(Guid organizationId, CancellationToken cancellationToken)
    {
        var detail = await tenantManagerService.GetTenantDetailAsync(organizationId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return View(new UpdateTenantManagementRequest(
            detail.Tenant.OrganizationId,
            detail.Tenant.OrganizationName,
            detail.Tenant.Status,
            detail.Tenant.SubscriptionPlan,
            detail.Tenant.CurrentVersion,
            detail.Tenant.HealthStatus,
            detail.Tenant.BillingStatus,
            detail.Tenant.ProvisioningStatus,
            detail.Tenant.TrialExpiresAtUtc));
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTenant(UpdateTenantManagementRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        var detail = await tenantManagerService.UpdateTenantAsync(request, PlatformUserId(), cancellationToken);
        return detail is null
            ? NotFound()
            : RedirectToAction(nameof(Tenant), new { organizationId = detail.Tenant.OrganizationId });
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TenantModules(UpdateTenantModulesRequest request, CancellationToken cancellationToken)
    {
        var detail = await tenantManagerService.UpdateTenantModulesAsync(request, PlatformUserId(), cancellationToken);
        return detail is null
            ? NotFound()
            : RedirectToAction(nameof(Tenant), new { organizationId = detail.Tenant.OrganizationId });
    }

    [Authorize(Roles = "Platform Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOwnerInvitation(Guid organizationId, CancellationToken cancellationToken)
    {
        var sent = await tenantManagerService.ResendOwnerInvitationAsync(organizationId, PlatformUserId(), cancellationToken);
        if (!sent)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Tenant), new { organizationId });
    }

    private string? PlatformUserId()
    {
        return User.FindFirstValue("platform_user_id");
    }

    private static ProvisionTenantRequest DefaultProvisionRequest()
    {
        return new ProvisionTenantRequest(
            BusinessName: string.Empty,
            BusinessEmail: string.Empty,
            OwnerName: string.Empty,
            OwnerEmail: string.Empty,
            Phone: string.Empty,
            AddressLine1: string.Empty,
            AddressLine2: null,
            City: string.Empty,
            Region: string.Empty,
            PostalCode: string.Empty,
            Country: "US",
            TimeZone: "America/Chicago",
            SubscriptionPlan: "Starter",
            IsTrial: true,
            DefaultModules: ["Service", "Parts"],
            DefaultLanguage: "en-US",
            DefaultCurrency: "USD");
    }
}
