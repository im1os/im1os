using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.Platform;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class SupplierItemSearchService(
    IApplicationDbContext dbContext,
    ITenantModuleEntitlementService tenantModuleEntitlements) : ISupplierItemSearchService
{
    public async Task<SupplierItemSearchPage> SearchAsync(string? query, int limit, CancellationToken cancellationToken)
    {
        return await SearchInternalAsync(null, new SupplierItemSearchRequest(query, null, null, null, null, null, SearchExecuted: Clean(query) is not null), limit, cancellationToken);
    }

    public async Task<SupplierItemSearchPage> SearchAsync(SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken)
    {
        return await SearchInternalAsync(null, request, limit, cancellationToken);
    }

    public async Task<SupplierItemSearchPage> SearchForCompanyAsync(Guid organizationId, SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken)
    {
        return await SearchInternalAsync(organizationId, request, limit, cancellationToken);
    }

    public async Task<int> CountFitmentItemsForCompanyAsync(Guid organizationId, string? vehicleType, int year, string make, string model, CancellationToken cancellationToken)
    {
        var cleanVehicleType = Clean(vehicleType);
        var cleanMake = Clean(make);
        var cleanModel = Clean(model);
        if (cleanMake is null || cleanModel is null)
        {
            return 0;
        }

        var enabledCompanySupplierCodes = await tenantModuleEntitlements.GetEnabledSupplierConnectorCodesAsync(organizationId, cancellationToken);
        var enabledCompanySupplierCodeList = enabledCompanySupplierCodes.ToArray();
        if (enabledCompanySupplierCodeList.Length == 0)
        {
            return 0;
        }

        var productIds = await (
                from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                join supplier in dbContext.Suppliers.AsNoTracking()
                    on fitment.SupplierId equals supplier.Id
                join supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    on new { fitment.SupplierId, fitment.SupplierSku } equals new { supplierProduct.SupplierId, supplierProduct.SupplierSku } into supplierProductJoin
                from supplierProduct in supplierProductJoin.DefaultIfEmpty()
                where fitment.Year == year &&
                    (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                    fitment.Make == cleanMake &&
                    fitment.Model == cleanModel &&
                    enabledCompanySupplierCodeList.Contains(supplier.Code)
                select fitment.SupplierProductId ?? (Guid?)supplierProduct.Id)
            .Where(x => x != null)
            .Distinct()
            .CountAsync(cancellationToken);

        return productIds;
    }

    private async Task<SupplierItemSearchPage> SearchInternalAsync(Guid? organizationId, SupplierItemSearchRequest request, int limit, CancellationToken cancellationToken)
    {
        var cleanQuery = Clean(request.Query);
        var cleanSupplierCode = Clean(request.SupplierCode)?.ToUpperInvariant();
        var cleanVehicleType = Clean(request.VehicleType);
        var cleanMake = Clean(request.Make);
        var cleanModel = Clean(request.Model);
        var selectedYear = request.Year;
        var normalizedPartQuery = ProductMatchingService.NormalizeManufacturerPartNumber(cleanQuery);
        var lowerQuery = cleanQuery?.ToLowerInvariant();
        var cappedLimit = Math.Clamp(limit, 1, 100);
        var offset = Math.Max(0, request.Offset);
        var hasTextSearch = cleanQuery is not null;
        var hasYmmSearch = selectedYear is not null && cleanMake is not null && cleanModel is not null;
        var hasSupplierFilter = cleanSupplierCode is not null;
        var isSearchExecuted = request.SearchExecuted || hasTextSearch || hasYmmSearch || hasSupplierFilter || offset > 0;
        var hasSearchCriteria = hasTextSearch || hasYmmSearch || hasSupplierFilter;
        var enabledCompanySupplierCodes = organizationId is null
            ? null
            : await tenantModuleEntitlements.GetEnabledSupplierConnectorCodesAsync(organizationId.Value, cancellationToken);
        var enabledCompanySupplierCodeList = enabledCompanySupplierCodes?.ToArray() ?? [];
        var requestedDisabledCompanySupplier = organizationId is not null &&
            cleanSupplierCode is not null &&
            enabledCompanySupplierCodes is not null &&
            !enabledCompanySupplierCodes.Contains(cleanSupplierCode);
        var configuredSupplierRows = organizationId is null
            ? await (
                    from configuration in dbContext.SupplierConnectorConfigurations.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on configuration.SupplierId equals supplier.Id
                    orderby supplier.Name
                    select new { supplier.Code, supplier.Name, configuration.IsEnabled })
                .Distinct()
                .ToListAsync(cancellationToken)
            : await (
                    from configuration in dbContext.CompanySupplierConnectorConfigurations.IgnoreQueryFilters().AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on configuration.SupplierId equals supplier.Id
                    where configuration.OrganizationId == organizationId.Value &&
                        enabledCompanySupplierCodeList.Contains(supplier.Code)
                    orderby supplier.Name
                    select new { supplier.Code, supplier.Name, configuration.IsEnabled })
                .Distinct()
                .ToListAsync(cancellationToken);
        var productSupplierRows = await (
                from supplier in dbContext.Suppliers.AsNoTracking()
                join supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    on supplier.Id equals supplierProduct.SupplierId
                where organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)
                select new { supplier.Code, supplier.Name })
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var productSuppliers = productSupplierRows
            .Select(x => new SupplierSearchOption(x.Code, x.Name, false, false))
            .ToList();
        var configuredSuppliers = organizationId is null
            ? configuredSupplierRows
                .Select(x => new SupplierSearchOption(x.Code, x.Name, true, x.IsEnabled))
                .ToList()
            : productSuppliers
                .Select(x =>
                {
                    var configured = configuredSupplierRows.FirstOrDefault(row => string.Equals(row.Code, x.Code, StringComparison.OrdinalIgnoreCase));
                    return new SupplierSearchOption(x.Code, x.Name, configured is not null, configured?.IsEnabled ?? false);
                })
                .ToList();
        var availableSuppliers = organizationId is null && configuredSuppliers.Count > 0
            ? configuredSuppliers
            : productSuppliers;
        var availableVehicleTypes = await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x =>
                x.VehicleType != null &&
                (organizationId == null || enabledCompanySupplierCodeList.Contains(x.SupplierKey) || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && enabledCompanySupplierCodeList.Contains(s.Code))) &&
                (!hasSupplierFilter || x.SupplierKey == cleanSupplierCode || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && s.Code == cleanSupplierCode)))
            .Select(x => x.VehicleType!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        var availableYears = await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x =>
                x.Year > 0 &&
                (cleanVehicleType == null || x.VehicleType == cleanVehicleType) &&
                (organizationId == null || enabledCompanySupplierCodeList.Contains(x.SupplierKey) || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && enabledCompanySupplierCodeList.Contains(s.Code))) &&
                (!hasSupplierFilter || x.SupplierKey == cleanSupplierCode || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && s.Code == cleanSupplierCode)))
            .Select(x => x.Year)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(cancellationToken);
        var availableMakes = selectedYear is null
            ? []
            : await dbContext.SupplierFitmentRecords
                .AsNoTracking()
                .Where(x =>
                    x.Year == selectedYear.Value &&
                    (cleanVehicleType == null || x.VehicleType == cleanVehicleType) &&
                    (organizationId == null || enabledCompanySupplierCodeList.Contains(x.SupplierKey) || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && enabledCompanySupplierCodeList.Contains(s.Code))) &&
                    (!hasSupplierFilter || x.SupplierKey == cleanSupplierCode || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && s.Code == cleanSupplierCode)))
                .Select(x => x.Make)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        var availableModels = selectedYear is null || cleanMake is null
            ? []
            : await dbContext.SupplierFitmentRecords
                .AsNoTracking()
                .Where(x =>
                    x.Year == selectedYear.Value &&
                    x.Make == cleanMake &&
                    (cleanVehicleType == null || x.VehicleType == cleanVehicleType) &&
                    (organizationId == null || enabledCompanySupplierCodeList.Contains(x.SupplierKey) || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && enabledCompanySupplierCodeList.Contains(s.Code))) &&
                    (!hasSupplierFilter || x.SupplierKey == cleanSupplierCode || dbContext.Suppliers.Any(s => s.Id == x.SupplierId && s.Code == cleanSupplierCode)))
                .Select(x => x.Model)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

        if (requestedDisabledCompanySupplier || !isSearchExecuted || !hasSearchCriteria)
        {
            return new SupplierItemSearchPage(
                cleanQuery,
                cleanSupplierCode,
                cleanVehicleType,
                selectedYear,
                cleanMake,
                cleanModel,
                availableSuppliers,
                configuredSuppliers,
                availableVehicleTypes,
                availableYears,
                availableMakes,
                availableModels,
                0,
                offset,
                cappedLimit,
                false,
                [],
                isSearchExecuted);
        }

        var rows = await (
                from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                    on supplierProduct.GlobalProductId equals globalProduct.Id
                join supplier in dbContext.Suppliers.AsNoTracking()
                    on supplierProduct.SupplierId equals supplier.Id
                where
                    (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                    (!hasSupplierFilter || supplier.Code == cleanSupplierCode) &&
                    (!hasTextSearch ||
                        supplierProduct.SupplierSku.ToLower().Contains(lowerQuery!) ||
                        (supplierProduct.ManufacturerPartNumber != null && supplierProduct.ManufacturerPartNumber.ToLower().Contains(lowerQuery!)) ||
                        (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber.Contains(normalizedPartQuery)) ||
                        (supplierProduct.SupplierDescription != null && supplierProduct.SupplierDescription.ToLower().Contains(lowerQuery!)) ||
                        globalProduct.Description.ToLower().Contains(lowerQuery!) ||
                        (globalProduct.LongDescription != null && globalProduct.LongDescription.ToLower().Contains(lowerQuery!))) &&
                    (!hasYmmSearch ||
                        dbContext.SupplierFitmentRecords.AsNoTracking().Any(fitment =>
                            (fitment.SupplierProductId == supplierProduct.Id || (fitment.SupplierId == supplier.Id && fitment.SupplierSku == supplierProduct.SupplierSku)) &&
                            (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                            fitment.Year == selectedYear!.Value &&
                            fitment.Make == cleanMake &&
                            fitment.Model == cleanModel))
                orderby supplier.Code, supplierProduct.SupplierSku
                select new
                {
                    SupplierProductId = supplierProduct.Id,
                    SupplierId = supplier.Id,
                    GlobalProductId = globalProduct.Id,
                    SupplierCode = supplier.Code,
                    SupplierName = supplier.Name,
                    supplierProduct.SupplierSku,
                    supplierProduct.ManufacturerPartNumber,
                    globalProduct.Upc,
                    globalProduct.Brand,
                    Title = supplierProduct.SupplierDescription ?? globalProduct.Description,
                    globalProduct.Category,
                    Status = supplierProduct.SupplierStatus,
                    ImageJson = supplierProduct.SupplierImagesJson ?? globalProduct.ImagesJson
                })
            .Skip(offset)
            .Take(cappedLimit + 1)
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > cappedLimit;
        if (hasMore)
        {
            rows = rows.Take(cappedLimit).ToList();
        }

        var supplierProductIds = rows.Select(x => x.SupplierProductId).ToArray();
        var supplierIds = rows.Select(x => x.SupplierId).Distinct().ToArray();
        var supplierSkus = rows.Select(x => x.SupplierSku).Distinct().ToArray();
        var priceRows = await dbContext.SupplierPrices
            .AsNoTracking()
            .Where(x => supplierProductIds.Contains(x.SupplierProductId))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken);
        var prices = priceRows
            .GroupBy(x => x.SupplierProductId)
            .ToDictionary(x => x.Key, x => x.First());
        var companyPrices = organizationId is null
            ? []
            : await dbContext.CompanySupplierPrices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId.Value && supplierProductIds.Contains(x.SupplierProductId))
                .ToDictionaryAsync(x => x.SupplierProductId, x => x.ActualDealerCost, cancellationToken);
        var fitmentRows = await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x =>
                (x.SupplierProductId != null && supplierProductIds.Contains(x.SupplierProductId.Value)) ||
                (supplierIds.Contains(x.SupplierId) && supplierSkus.Contains(x.SupplierSku)))
            .Select(x => new
            {
                x.Id,
                x.SupplierProductId,
                x.SupplierId,
                x.SupplierSku
            })
            .ToListAsync(cancellationToken);

        var results = rows
            .Select(row =>
            {
                prices.TryGetValue(row.SupplierProductId, out var price);
                companyPrices.TryGetValue(row.SupplierProductId, out var actualCost);
                return new SupplierItemSearchResult(
                    row.SupplierProductId,
                    row.GlobalProductId,
                    row.SupplierCode,
                    row.SupplierName,
                    row.SupplierSku,
                    row.ManufacturerPartNumber,
                    row.Upc,
                    row.Brand,
                    row.Title,
                    row.Category,
                    row.Status,
                    fitmentRows
                        .Where(x => x.SupplierProductId == row.SupplierProductId || (x.SupplierId == row.SupplierId && x.SupplierSku == row.SupplierSku))
                        .Select(x => x.Id)
                        .Distinct()
                        .Count(),
                    price?.Msrp,
                    price?.DealerCost,
                    companyPrices.ContainsKey(row.SupplierProductId) ? actualCost : null,
                    FirstImageUrl(row.ImageJson));
            })
            .ToList();

        return new SupplierItemSearchPage(
            cleanQuery,
            cleanSupplierCode,
            cleanVehicleType,
            selectedYear,
            cleanMake,
            cleanModel,
            availableSuppliers,
            configuredSuppliers,
            availableVehicleTypes,
            availableYears,
            availableMakes,
            availableModels,
            offset + results.Count,
            offset,
            cappedLimit,
            hasMore,
            results,
            isSearchExecuted);
    }

    private static string? FirstImageUrl(string? imageJson)
    {
        if (string.IsNullOrWhiteSpace(imageJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(imageJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                    {
                        return Clean(url.GetString());
                    }
                }
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("url", out var objectUrl) &&
                objectUrl.ValueKind == JsonValueKind.String)
            {
                return Clean(objectUrl.GetString());
            }
        }
        catch (JsonException)
        {
            return Clean(imageJson);
        }

        return null;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
