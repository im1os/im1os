using System.Diagnostics;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class SupplierItemSearchService(
    IApplicationDbContext dbContext,
    ITenantModuleEntitlementService tenantModuleEntitlements,
    IMemoryCache memoryCache,
    ILogger<SupplierItemSearchService> logger) : ISupplierItemSearchService
{
    private static readonly TimeSpan YmmFacetCacheDuration = TimeSpan.FromHours(6);

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
        var totalStopwatch = Stopwatch.StartNew();
        var timingSteps = new List<SearchTimingStep>();
        async Task<T> TimedAsync<T>(string name, Func<Task<T>> action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return await action();
            }
            finally
            {
                timingSteps.Add(new SearchTimingStep(name, stopwatch.ElapsedMilliseconds));
            }
        }

        async Task<IReadOnlyCollection<T>> TimedCachedFacetAsync<T>(string name, string cacheKey, Func<Task<List<T>>> action)
        {
            return await TimedAsync(name, async () =>
            {
                if (memoryCache.TryGetValue<IReadOnlyCollection<T>>(cacheKey, out var cached) && cached is not null)
                {
                    return cached;
                }

                var value = await action();
                memoryCache.Set<IReadOnlyCollection<T>>(cacheKey, value, YmmFacetCacheDuration);
                return value;
            });
        }

        T Timed<T>(string name, Func<T> action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return action();
            }
            finally
            {
                timingSteps.Add(new SearchTimingStep(name, stopwatch.ElapsedMilliseconds));
            }
        }

        var cleanQuery = Clean(request.Query);
        var cleanSupplierCode = Clean(request.SupplierCode)?.ToUpperInvariant();
        var cleanVehicleType = Clean(request.VehicleType);
        var cleanMake = Clean(request.Make);
        var cleanModel = Clean(request.Model);
        var selectedYear = request.Year;
        var normalizedPartQuery = ProductMatchingService.NormalizeManufacturerPartNumber(cleanQuery);
        var lowerQuery = cleanQuery?.ToLowerInvariant();
        var likeQuery = cleanQuery is null ? null : $"%{cleanQuery}%";
        var prefixLikeQuery = cleanQuery is null ? null : $"{cleanQuery}%";
        var cappedLimit = Math.Clamp(limit, 1, 100);
        var offset = Math.Max(0, request.Offset);
        var hasTextSearch = cleanQuery is not null;
        var isIdentifierSearch = IsLikelyCatalogIdentifier(cleanQuery);
        var hasYmmSearch = selectedYear is not null && cleanMake is not null && cleanModel is not null;
        var hasSupplierFilter = cleanSupplierCode is not null;
        var isSearchExecuted = request.SearchExecuted || hasTextSearch || hasYmmSearch || hasSupplierFilter || offset > 0;
        var hasSearchCriteria = hasTextSearch || hasYmmSearch || hasSupplierFilter;
        var includeFacets = request.IncludeFacets && !hasTextSearch;
        var includeYmmFacets = includeFacets;
        var usePostgresLike = dbContext is DbContext efDbContext &&
            efDbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        var enabledCompanySupplierCodes = organizationId is null
            ? null
            : await TimedAsync("company-supplier-entitlements", () => tenantModuleEntitlements.GetEnabledSupplierConnectorCodesAsync(organizationId.Value, cancellationToken));
        var enabledCompanySupplierCodeList = enabledCompanySupplierCodes?.ToArray() ?? [];
        var facetScopeCacheKey = organizationId is null
            ? "platform"
            : $"company:{organizationId.Value:N}:{string.Join("-", enabledCompanySupplierCodeList.Order(StringComparer.OrdinalIgnoreCase))}";
        var facetCachePrefix = $"supplier-item-search:ymm-facets:v3:{facetScopeCacheKey}:supplier:{cleanSupplierCode ?? "*"}";
        var requestedDisabledCompanySupplier = organizationId is not null &&
            cleanSupplierCode is not null &&
            enabledCompanySupplierCodes is not null &&
            !enabledCompanySupplierCodes.Contains(cleanSupplierCode);
        IReadOnlyCollection<SupplierSearchOption> availableSuppliers = [];
        IReadOnlyCollection<SupplierSearchOption> configuredSuppliers = [];
        IReadOnlyCollection<string> availableVehicleTypes = [];
        IReadOnlyCollection<int> availableYears = [];
        IReadOnlyCollection<string> availableMakes = [];
        IReadOnlyCollection<string> availableModels = [];
        if (includeFacets)
        {
            var configuredSupplierRows = organizationId is null
                ? await TimedAsync("facets-configured-suppliers", () => (
                        from configuration in dbContext.SupplierConnectorConfigurations.AsNoTracking()
                        join supplier in dbContext.Suppliers.AsNoTracking()
                            on configuration.SupplierId equals supplier.Id
                        orderby supplier.Name
                        select new { supplier.Code, supplier.Name, configuration.IsEnabled })
                    .Distinct()
                    .ToListAsync(cancellationToken))
                : await TimedAsync("facets-company-configured-suppliers", () => (
                        from configuration in dbContext.CompanySupplierConnectorConfigurations.IgnoreQueryFilters().AsNoTracking()
                        join supplier in dbContext.Suppliers.AsNoTracking()
                            on configuration.SupplierId equals supplier.Id
                        where configuration.OrganizationId == organizationId.Value &&
                            enabledCompanySupplierCodeList.Contains(supplier.Code)
                        orderby supplier.Name
                        select new { supplier.Code, supplier.Name, configuration.IsEnabled })
                    .Distinct()
                    .ToListAsync(cancellationToken));
            var productSupplierRows = await TimedAsync("facets-product-suppliers", () => (
                    from supplier in dbContext.Suppliers.AsNoTracking()
                    join supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                        on supplier.Id equals supplierProduct.SupplierId
                    where organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)
                    select new { supplier.Code, supplier.Name })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken));
            var productSuppliers = productSupplierRows
                .Select(x => new SupplierSearchOption(x.Code, x.Name, false, false))
                .ToList();
            configuredSuppliers = organizationId is null
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
            availableSuppliers = organizationId is null && configuredSuppliers.Count > 0
                ? configuredSuppliers
                : productSuppliers;
            availableVehicleTypes = includeYmmFacets
                ? await TimedCachedFacetAsync("facets-vehicle-types", $"{facetCachePrefix}:vehicle-types", () => (
                    from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on fitment.SupplierId equals supplier.Id
                    where
                        fitment.VehicleType != null &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select fitment.VehicleType!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken))
                : [];
            availableYears = includeYmmFacets
                ? await TimedCachedFacetAsync("facets-years", $"{facetCachePrefix}:years:vehicle-type:{cleanVehicleType ?? "*"}", () => (
                    from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on fitment.SupplierId equals supplier.Id
                    where
                        fitment.Year > 0 &&
                        (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select fitment.Year)
                    .Distinct()
                    .OrderByDescending(x => x)
                    .ToListAsync(cancellationToken))
                : [];
            availableMakes = !includeYmmFacets || selectedYear is null
                ? []
                : await TimedCachedFacetAsync("facets-makes", $"{facetCachePrefix}:makes:vehicle-type:{cleanVehicleType ?? "*"}:year:{selectedYear.Value}", () => (
                    from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on fitment.SupplierId equals supplier.Id
                    where
                        fitment.Year == selectedYear.Value &&
                        (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select fitment.Make)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken));
            availableModels = !includeYmmFacets || selectedYear is null || cleanMake is null
                ? []
                : await TimedCachedFacetAsync("facets-models", $"{facetCachePrefix}:models:vehicle-type:{cleanVehicleType ?? "*"}:year:{selectedYear.Value}:make:{cleanMake}", () => (
                    from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on fitment.SupplierId equals supplier.Id
                    where
                        fitment.Year == selectedYear.Value &&
                        fitment.Make == cleanMake &&
                        (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select fitment.Model)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync(cancellationToken));
        }

        if (requestedDisabledCompanySupplier || !isSearchExecuted || !hasSearchCriteria)
        {
            LogSearchTiming("not-executed", 0, 0, 0, 0);
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

        var identifierCandidateIds = Array.Empty<Guid>();
        if (isIdentifierSearch && hasTextSearch && !hasYmmSearch)
        {
            var supplierFieldCandidateIds = await TimedAsync("identifier-supplier-field-candidates", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    where
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode) &&
                        ((usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, cleanQuery!) : supplierProduct.SupplierSku.ToLower() == lowerQuery!) ||
                        (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, prefixLikeQuery!) : supplierProduct.SupplierSku.ToLower().StartsWith(lowerQuery!)) ||
                        (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, cleanQuery!) : supplierProduct.SupplierPartNumber!.ToLower() == lowerQuery!)) ||
                        (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, prefixLikeQuery!) : supplierProduct.SupplierPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                        (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, cleanQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower() == lowerQuery!)) ||
                        (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, prefixLikeQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                        (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.StartsWith(normalizedPartQuery)) ||
                        (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, likeQuery!) : supplierProduct.SupplierSku.ToLower().Contains(lowerQuery!)) ||
                        (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, likeQuery!) : supplierProduct.SupplierPartNumber!.ToLower().Contains(lowerQuery!))) ||
                        (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, likeQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower().Contains(lowerQuery!))) ||
                        (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery)))
                    select supplierProduct.Id)
                .Distinct()
                .Take(1000)
                .ToListAsync(cancellationToken));
            var globalFieldCandidateIds = await TimedAsync("identifier-global-field-candidates", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                        on supplierProduct.GlobalProductId equals globalProduct.Id
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    where
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode) &&
                        ((globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, cleanQuery!) : globalProduct.ManufacturerPartNumber!.ToLower() == lowerQuery!)) ||
                        (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, prefixLikeQuery!) : globalProduct.ManufacturerPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                        (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.StartsWith(normalizedPartQuery)) ||
                        (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, likeQuery!) : globalProduct.ManufacturerPartNumber!.ToLower().Contains(lowerQuery!))) ||
                        (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery)))
                    select supplierProduct.Id)
                .Distinct()
                .Take(1000)
                .ToListAsync(cancellationToken));
            identifierCandidateIds = supplierFieldCandidateIds
                .Concat(globalFieldCandidateIds)
                .Distinct()
                .ToArray();
        }
        var ymmCandidateIds = Array.Empty<Guid>();
        if (hasYmmSearch)
        {
            var directYmmCandidateIds = await TimedAsync("ymm-direct-fitment-candidates", () => (
                    from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on fitment.SupplierId equals supplier.Id
                    where
                        fitment.SupplierProductId != null &&
                        fitment.Year == selectedYear!.Value &&
                        fitment.Make == cleanMake &&
                        fitment.Model == cleanModel &&
                        (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select fitment.SupplierProductId!.Value)
                .Distinct()
                .Take(10000)
                .ToListAsync(cancellationToken));
            var skuYmmCandidateIds = await TimedAsync("ymm-sku-fitment-candidates", () => (
                    from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on fitment.SupplierId equals supplier.Id
                    join supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                        on new { fitment.SupplierId, fitment.SupplierSku } equals new { supplierProduct.SupplierId, supplierProduct.SupplierSku }
                    where
                        fitment.Year == selectedYear!.Value &&
                        fitment.Make == cleanMake &&
                        fitment.Model == cleanModel &&
                        (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select supplierProduct.Id)
                .Distinct()
                .Take(10000)
                .ToListAsync(cancellationToken));
            ymmCandidateIds = directYmmCandidateIds
                .Concat(skuYmmCandidateIds)
                .Distinct()
                .ToArray();
        }

        var rows = isIdentifierSearch && hasTextSearch && !hasYmmSearch
            ? await TimedAsync("identifier-item-query", () => (
                from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                    on supplierProduct.GlobalProductId equals globalProduct.Id
                join supplier in dbContext.Suppliers.AsNoTracking()
                    on supplierProduct.SupplierId equals supplier.Id
                let baseTitle = supplierProduct.SupplierDescription ?? globalProduct.Description
                let maker = globalProduct.Manufacturer ?? globalProduct.Brand
                let displayPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber
                where identifierCandidateIds.Contains(supplierProduct.Id)
                orderby
                    supplierProduct.SupplierSku.ToLower() == lowerQuery ? 0 :
                    supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower() == lowerQuery ? 1 :
                    displayPartNumber != null && displayPartNumber!.ToLower() == lowerQuery ? 1 :
                    supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber! == normalizedPartQuery ? 1 :
                    globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber! == normalizedPartQuery ? 1 :
                    supplierProduct.SupplierSku.ToLower().StartsWith(lowerQuery!) ? 3 :
                    supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower().StartsWith(lowerQuery!) ? 4 :
                    displayPartNumber != null && displayPartNumber!.ToLower().StartsWith(lowerQuery!) ? 4 :
                    supplierProduct.SupplierSku.ToLower().Contains(lowerQuery!) ? 6 :
                    supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower().Contains(lowerQuery!) ? 7 :
                    displayPartNumber != null && displayPartNumber!.ToLower().Contains(lowerQuery!) ? 7 :
                    supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery) ? 7 :
                    globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery) ? 7 :
                    10,
                    maker,
                    displayPartNumber,
                    supplier.Code,
                    supplierProduct.SupplierSku
                select new
                {
                    SupplierProductId = supplierProduct.Id,
                    SupplierId = supplier.Id,
                    GlobalProductId = globalProduct.Id,
                    SupplierCode = supplier.Code,
                    SupplierName = supplier.Name,
                    supplierProduct.SupplierSku,
                    ManufacturerPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber,
                    NormalizedManufacturerPartNumber = supplierProduct.NormalizedManufacturerPartNumber ?? globalProduct.NormalizedManufacturerPartNumber,
                    globalProduct.Upc,
                    globalProduct.Brand,
                    globalProduct.Manufacturer,
                    supplierProduct.SupplierDescription,
                    GlobalDescription = globalProduct.Description,
                    globalProduct.Category,
                    Status = supplierProduct.SupplierStatus,
                    supplierProduct.WarehouseAvailability,
                    ImageJson = supplierProduct.SupplierImagesJson ?? globalProduct.ImagesJson
                })
            .Skip(offset)
            .Take(cappedLimit + 1)
            .ToListAsync(cancellationToken))
            : hasYmmSearch
            ? await TimedAsync(hasTextSearch ? "ymm-filtered-item-query" : "ymm-item-query", () => (
                from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                    on supplierProduct.GlobalProductId equals globalProduct.Id
                join supplier in dbContext.Suppliers.AsNoTracking()
                    on supplierProduct.SupplierId equals supplier.Id
                let baseTitle = supplierProduct.SupplierDescription ?? globalProduct.Description
                let maker = globalProduct.Manufacturer ?? globalProduct.Brand
                let displayPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber
                let formattedTitle = maker + " " + baseTitle + " - " + (displayPartNumber ?? string.Empty)
                where
                    ymmCandidateIds.Contains(supplierProduct.Id) &&
                    (!hasTextSearch ||
                        (isIdentifierSearch &&
                            ((usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, cleanQuery!) : supplierProduct.SupplierSku.ToLower() == lowerQuery!) ||
                            (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, prefixLikeQuery!) : supplierProduct.SupplierSku.ToLower().StartsWith(lowerQuery!)) ||
                            (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, cleanQuery!) : supplierProduct.SupplierPartNumber!.ToLower() == lowerQuery!)) ||
                            (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, prefixLikeQuery!) : supplierProduct.SupplierPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                            (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, cleanQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower() == lowerQuery!)) ||
                            (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, prefixLikeQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                            (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.StartsWith(normalizedPartQuery)) ||
                            (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, cleanQuery!) : globalProduct.ManufacturerPartNumber!.ToLower() == lowerQuery!)) ||
                            (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, prefixLikeQuery!) : globalProduct.ManufacturerPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                            (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.StartsWith(normalizedPartQuery)))) ||
                        (!isIdentifierSearch &&
                            ((usePostgresLike && EF.Functions.ToTsVector(
                                "english",
                                (supplierProduct.SupplierSku ?? string.Empty) + " " +
                                (supplierProduct.SupplierPartNumber ?? string.Empty) + " " +
                                (supplierProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                                (supplierProduct.SupplierDescription ?? string.Empty) + " " +
                                (globalProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                                (globalProduct.Manufacturer ?? string.Empty) + " " +
                                globalProduct.Brand + " " +
                                globalProduct.Description + " " +
                                (globalProduct.Category ?? string.Empty) + " " +
                                (globalProduct.LongDescription ?? string.Empty))
                                .Matches(EF.Functions.PlainToTsQuery("english", cleanQuery!))) ||
                            (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, likeQuery!) : supplierProduct.SupplierSku.ToLower().Contains(lowerQuery!)) ||
                            (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, likeQuery!) : supplierProduct.SupplierPartNumber!.ToLower().Contains(lowerQuery!))) ||
                            (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, likeQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower().Contains(lowerQuery!))) ||
                            (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery)) ||
                            (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, likeQuery!) : globalProduct.ManufacturerPartNumber!.ToLower().Contains(lowerQuery!))) ||
                            (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery)) ||
                            (supplierProduct.SupplierDescription != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierDescription!, likeQuery!) : supplierProduct.SupplierDescription!.ToLower().Contains(lowerQuery!))) ||
                            (globalProduct.Manufacturer != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.Manufacturer!, likeQuery!) : globalProduct.Manufacturer!.ToLower().Contains(lowerQuery!))) ||
                            (usePostgresLike ? EF.Functions.ILike(globalProduct.Brand, likeQuery!) : globalProduct.Brand.ToLower().Contains(lowerQuery!)) ||
                            (usePostgresLike ? EF.Functions.ILike(globalProduct.Description, likeQuery!) : globalProduct.Description.ToLower().Contains(lowerQuery!)) ||
                            (usePostgresLike ? EF.Functions.ILike(formattedTitle, likeQuery!) : formattedTitle.ToLower().Contains(lowerQuery!)) ||
                            (globalProduct.LongDescription != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.LongDescription!, likeQuery!) : globalProduct.LongDescription!.ToLower().Contains(lowerQuery!))))))
                orderby
                    !hasTextSearch ? 100 :
                        supplierProduct.SupplierSku.ToLower() == lowerQuery ? 0 :
                        supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower() == lowerQuery ? 1 :
                        displayPartNumber != null && displayPartNumber!.ToLower() == lowerQuery ? 1 :
                        supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber! == normalizedPartQuery ? 1 :
                        globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber! == normalizedPartQuery ? 1 :
                        baseTitle.ToLower() == lowerQuery! ? 2 :
                        formattedTitle.ToLower() == lowerQuery! ? 2 :
                        supplierProduct.SupplierSku.ToLower().StartsWith(lowerQuery!) ? 3 :
                        supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower().StartsWith(lowerQuery!) ? 4 :
                        displayPartNumber != null && displayPartNumber!.ToLower().StartsWith(lowerQuery!) ? 4 :
                        baseTitle.ToLower().StartsWith(lowerQuery!) ? 5 :
                        formattedTitle.ToLower().StartsWith(lowerQuery!) ? 5 :
                        10,
                    maker,
                    displayPartNumber,
                    supplier.Code,
                    supplierProduct.SupplierSku
                select new
                {
                    SupplierProductId = supplierProduct.Id,
                    SupplierId = supplier.Id,
                    GlobalProductId = globalProduct.Id,
                    SupplierCode = supplier.Code,
                    SupplierName = supplier.Name,
                    supplierProduct.SupplierSku,
                    ManufacturerPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber,
                    NormalizedManufacturerPartNumber = supplierProduct.NormalizedManufacturerPartNumber ?? globalProduct.NormalizedManufacturerPartNumber,
                    globalProduct.Upc,
                    globalProduct.Brand,
                    globalProduct.Manufacturer,
                    supplierProduct.SupplierDescription,
                    GlobalDescription = globalProduct.Description,
                    globalProduct.Category,
                    Status = supplierProduct.SupplierStatus,
                    supplierProduct.WarehouseAvailability,
                    ImageJson = supplierProduct.SupplierImagesJson ?? globalProduct.ImagesJson
                })
            .Skip(offset)
            .Take(cappedLimit + 1)
            .ToListAsync(cancellationToken))
            : await TimedAsync("main-item-query", () => (
                from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                    on supplierProduct.GlobalProductId equals globalProduct.Id
                join supplier in dbContext.Suppliers.AsNoTracking()
                    on supplierProduct.SupplierId equals supplier.Id
                let baseTitle = supplierProduct.SupplierDescription ?? globalProduct.Description
                let maker = globalProduct.Manufacturer ?? globalProduct.Brand
                let displayPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber
                let formattedTitle = maker + " " + baseTitle + " - " + (displayPartNumber ?? string.Empty)
                where
                    (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                    (!hasSupplierFilter || supplier.Code == cleanSupplierCode) &&
                    (!hasTextSearch ||
                        (isIdentifierSearch &&
                            ((usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, cleanQuery!) : supplierProduct.SupplierSku.ToLower() == lowerQuery!) ||
                            (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, prefixLikeQuery!) : supplierProduct.SupplierSku.ToLower().StartsWith(lowerQuery!)) ||
                            (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, cleanQuery!) : supplierProduct.SupplierPartNumber!.ToLower() == lowerQuery!)) ||
                            (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, prefixLikeQuery!) : supplierProduct.SupplierPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                            (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, cleanQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower() == lowerQuery!)) ||
                            (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, prefixLikeQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                            (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.StartsWith(normalizedPartQuery)) ||
                            (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, cleanQuery!) : globalProduct.ManufacturerPartNumber!.ToLower() == lowerQuery!)) ||
                            (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, prefixLikeQuery!) : globalProduct.ManufacturerPartNumber!.ToLower().StartsWith(lowerQuery!))) ||
                            (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.StartsWith(normalizedPartQuery)))) ||
                        (!isIdentifierSearch &&
                            ((usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierSku, likeQuery!) : supplierProduct.SupplierSku.ToLower().Contains(lowerQuery!)) ||
                            (supplierProduct.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierPartNumber!, likeQuery!) : supplierProduct.SupplierPartNumber!.ToLower().Contains(lowerQuery!))) ||
                            (supplierProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.ManufacturerPartNumber!, likeQuery!) : supplierProduct.ManufacturerPartNumber!.ToLower().Contains(lowerQuery!))) ||
                            (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery)) ||
                            (globalProduct.ManufacturerPartNumber != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.ManufacturerPartNumber!, likeQuery!) : globalProduct.ManufacturerPartNumber!.ToLower().Contains(lowerQuery!))) ||
                            (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber!.Contains(normalizedPartQuery)) ||
                            (supplierProduct.SupplierDescription != null && (usePostgresLike ? EF.Functions.ILike(supplierProduct.SupplierDescription!, likeQuery!) : supplierProduct.SupplierDescription!.ToLower().Contains(lowerQuery!))) ||
                            (globalProduct.Manufacturer != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.Manufacturer!, likeQuery!) : globalProduct.Manufacturer!.ToLower().Contains(lowerQuery!))) ||
                            (usePostgresLike ? EF.Functions.ILike(globalProduct.Brand, likeQuery!) : globalProduct.Brand.ToLower().Contains(lowerQuery!)) ||
                            (usePostgresLike ? EF.Functions.ILike(globalProduct.Description, likeQuery!) : globalProduct.Description.ToLower().Contains(lowerQuery!)) ||
                            (usePostgresLike ? EF.Functions.ILike(formattedTitle, likeQuery!) : formattedTitle.ToLower().Contains(lowerQuery!)) ||
                            (globalProduct.LongDescription != null && (usePostgresLike ? EF.Functions.ILike(globalProduct.LongDescription!, likeQuery!) : globalProduct.LongDescription!.ToLower().Contains(lowerQuery!)))))) &&
                    (!hasYmmSearch ||
                        dbContext.SupplierFitmentRecords.AsNoTracking().Any(fitment =>
                            (fitment.SupplierProductId == supplierProduct.Id || (fitment.SupplierId == supplier.Id && fitment.SupplierSku == supplierProduct.SupplierSku)) &&
                            (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                            fitment.Year == selectedYear!.Value &&
                            fitment.Make == cleanMake &&
                            fitment.Model == cleanModel))
                orderby
                    !hasTextSearch ? 100 :
                        supplierProduct.SupplierSku.ToLower() == lowerQuery ? 0 :
                        supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower() == lowerQuery ? 1 :
                        displayPartNumber != null && displayPartNumber!.ToLower() == lowerQuery ? 1 :
                        supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && supplierProduct.NormalizedManufacturerPartNumber! == normalizedPartQuery ? 1 :
                        globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && globalProduct.NormalizedManufacturerPartNumber! == normalizedPartQuery ? 1 :
                        baseTitle.ToLower() == lowerQuery! ? 2 :
                        formattedTitle.ToLower() == lowerQuery! ? 2 :
                        supplierProduct.SupplierSku.ToLower().StartsWith(lowerQuery!) ? 3 :
                        supplierProduct.SupplierPartNumber != null && supplierProduct.SupplierPartNumber!.ToLower().StartsWith(lowerQuery!) ? 4 :
                        displayPartNumber != null && displayPartNumber!.ToLower().StartsWith(lowerQuery!) ? 4 :
                        baseTitle.ToLower().StartsWith(lowerQuery!) ? 5 :
                        formattedTitle.ToLower().StartsWith(lowerQuery!) ? 5 :
                        10,
                    maker,
                    displayPartNumber,
                    supplier.Code,
                    supplierProduct.SupplierSku
                select new
                {
                    SupplierProductId = supplierProduct.Id,
                    SupplierId = supplier.Id,
                    GlobalProductId = globalProduct.Id,
                    SupplierCode = supplier.Code,
                    SupplierName = supplier.Name,
                    supplierProduct.SupplierSku,
                    ManufacturerPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber,
                    NormalizedManufacturerPartNumber = supplierProduct.NormalizedManufacturerPartNumber ?? globalProduct.NormalizedManufacturerPartNumber,
                    globalProduct.Upc,
                    globalProduct.Brand,
                    globalProduct.Manufacturer,
                    supplierProduct.SupplierDescription,
                    GlobalDescription = globalProduct.Description,
                    globalProduct.Category,
                    Status = supplierProduct.SupplierStatus,
                    supplierProduct.WarehouseAvailability,
                    ImageJson = supplierProduct.SupplierImagesJson ?? globalProduct.ImagesJson
                })
            .Skip(offset)
            .Take(cappedLimit + 1)
            .ToListAsync(cancellationToken));
        var hasMore = rows.Count > cappedLimit;
        if (hasMore)
        {
            rows = rows.Take(cappedLimit).ToList();
        }

        var supplierProductIds = rows.Select(x => x.SupplierProductId).ToArray();
        var supplierIds = rows.Select(x => x.SupplierId).Distinct().ToArray();
        var supplierSkus = rows.Select(x => x.SupplierSku).Distinct().ToArray();
        var resultPartNumbers = Timed("prepare-result-part-numbers", () => rows
            .Select(x => new
            {
                x.SupplierProductId,
                NormalizedManufacturerPartNumber = Clean(x.NormalizedManufacturerPartNumber) ?? ProductMatchingService.NormalizeManufacturerPartNumber(x.ManufacturerPartNumber)
            })
            .Where(x => x.NormalizedManufacturerPartNumber is not null)
            .ToList());
        var normalizedResultPartNumbers = resultPartNumbers
            .Select(x => x.NormalizedManufacturerPartNumber!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resultManufacturerPartNumbers = rows
            .Select(x => Clean(x.ManufacturerPartNumber))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var priceRows = await TimedAsync("supplier-prices", () => dbContext.SupplierPrices
            .AsNoTracking()
            .Where(x => supplierProductIds.Contains(x.SupplierProductId))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken));
        var prices = Timed("supplier-prices-grouping", () => priceRows
            .GroupBy(x => x.SupplierProductId)
            .ToDictionary(x => x.Key, x => x.First()));
        var companyPrices = organizationId is null
            ? []
            : await TimedAsync("company-prices", () => dbContext.CompanySupplierPrices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId.Value && supplierProductIds.Contains(x.SupplierProductId))
                .ToDictionaryAsync(x => x.SupplierProductId, x => x.ActualDealerCost, cancellationToken));
        var fitmentRows = await TimedAsync("fitment-for-results", () => dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x =>
                (x.SupplierProductId != null && supplierProductIds.Contains(x.SupplierProductId.Value)) ||
                (supplierIds.Contains(x.SupplierId) && supplierSkus.Contains(x.SupplierSku)))
            .Select(x => new FitmentSourceRow(
                x.Id,
                x.SupplierProductId,
                x.SupplierId,
                x.SupplierKey,
                x.SupplierSku,
                x.Year,
                x.Make,
                x.Model,
                x.Submodel,
                x.Engine,
                x.Notes,
                x.VehicleType))
            .ToListAsync(cancellationToken));
        var crossReferenceCandidateIds = Array.Empty<Guid>();
        if (normalizedResultPartNumbers.Length > 0 || resultManufacturerPartNumbers.Length > 0)
        {
            var supplierCrossReferenceCandidateIds = await TimedAsync("cross-reference-supplier-field-candidates", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    where
                        !supplierProductIds.Contains(supplierProduct.Id) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        ((supplierProduct.NormalizedManufacturerPartNumber != null && normalizedResultPartNumbers.Contains(supplierProduct.NormalizedManufacturerPartNumber)) ||
                            (supplierProduct.ManufacturerPartNumber != null && resultManufacturerPartNumbers.Contains(supplierProduct.ManufacturerPartNumber)))
                    select supplierProduct.Id)
                .Distinct()
                .Take(1000)
                .ToListAsync(cancellationToken));
            var globalCrossReferenceCandidateIds = await TimedAsync("cross-reference-global-field-candidates", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                        on supplierProduct.GlobalProductId equals globalProduct.Id
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    where
                        !supplierProductIds.Contains(supplierProduct.Id) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        ((globalProduct.NormalizedManufacturerPartNumber != null && normalizedResultPartNumbers.Contains(globalProduct.NormalizedManufacturerPartNumber)) ||
                            (globalProduct.ManufacturerPartNumber != null && resultManufacturerPartNumbers.Contains(globalProduct.ManufacturerPartNumber)))
                    select supplierProduct.Id)
                .Distinct()
                .Take(1000)
                .ToListAsync(cancellationToken));
            crossReferenceCandidateIds = supplierCrossReferenceCandidateIds
                .Concat(globalCrossReferenceCandidateIds)
                .Distinct()
                .ToArray();
        }

        IReadOnlyList<CrossReferenceRow> crossReferenceRows = crossReferenceCandidateIds.Length == 0
            ? Array.Empty<CrossReferenceRow>()
            : await TimedAsync("cross-reference-query", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                        on supplierProduct.GlobalProductId equals globalProduct.Id
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    where crossReferenceCandidateIds.Contains(supplierProduct.Id)
                    orderby supplier.Code, supplierProduct.SupplierSku
                    select new CrossReferenceRow(
                        supplierProduct.NormalizedManufacturerPartNumber ?? globalProduct.NormalizedManufacturerPartNumber,
                        supplierProduct.Id,
                        supplier.Id,
                        globalProduct.Id,
                        supplier.Code,
                        supplier.Name,
                        supplierProduct.SupplierSku,
                        supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber,
                        globalProduct.Upc,
                        globalProduct.Brand,
                        globalProduct.Manufacturer,
                        supplierProduct.SupplierDescription,
                        globalProduct.Description,
                        globalProduct.Category,
                        supplierProduct.WarehouseAvailability,
                        supplierProduct.SupplierImagesJson ?? globalProduct.ImagesJson,
                        supplierProduct.SupplierStatus))
                .ToListAsync(cancellationToken));
        var crossReferenceSupplierProductIds = crossReferenceRows.Select(x => x.SupplierProductId).Distinct().ToArray();
        var crossReferenceSupplierIds = crossReferenceRows.Select(x => x.SupplierId).Distinct().ToArray();
        var crossReferenceSupplierSkus = crossReferenceRows.Select(x => x.SupplierSku).Distinct().ToArray();
        var crossReferencePriceRows = await TimedAsync("cross-reference-prices", () => dbContext.SupplierPrices
            .AsNoTracking()
            .Where(x => crossReferenceSupplierProductIds.Contains(x.SupplierProductId))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken));
        var crossReferencePrices = Timed("cross-reference-prices-grouping", () => crossReferencePriceRows
            .GroupBy(x => x.SupplierProductId)
            .ToDictionary(x => x.Key, x => x.First()));
        var crossReferenceCompanyPrices = organizationId is null || crossReferenceSupplierProductIds.Length == 0
            ? []
            : await TimedAsync("cross-reference-company-prices", () => dbContext.CompanySupplierPrices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId.Value && crossReferenceSupplierProductIds.Contains(x.SupplierProductId))
                .ToDictionaryAsync(x => x.SupplierProductId, x => x.ActualDealerCost, cancellationToken));
        var crossReferenceFitmentRows = await TimedAsync("cross-reference-fitment", () => dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x =>
                (x.SupplierProductId != null && crossReferenceSupplierProductIds.Contains(x.SupplierProductId.Value)) ||
                (crossReferenceSupplierIds.Contains(x.SupplierId) && crossReferenceSupplierSkus.Contains(x.SupplierSku)))
            .Select(x => new FitmentSourceRow(
                x.Id,
                x.SupplierProductId,
                x.SupplierId,
                x.SupplierKey,
                x.SupplierSku,
                x.Year,
                x.Make,
                x.Model,
                x.Submodel,
                x.Engine,
                x.Notes,
                x.VehicleType))
            .ToListAsync(cancellationToken));

        var offerCandidates = Timed("build-offer-candidates", () => rows
            .Select((row, index) =>
            {
                prices.TryGetValue(row.SupplierProductId, out var price);
                companyPrices.TryGetValue(row.SupplierProductId, out var actualCost);
                return new OfferCandidate(
                    GroupKey(row.NormalizedManufacturerPartNumber, row.ManufacturerPartNumber, row.GlobalProductId, row.SupplierProductId),
                    index,
                    false,
                    new SupplierItemOfferResult(
                        row.SupplierProductId,
                        row.GlobalProductId,
                        row.SupplierCode,
                        row.SupplierName,
                        row.SupplierSku,
                        row.ManufacturerPartNumber,
                        row.Upc,
                        row.Brand,
                        BuildDisplayTitle(
                            row.Manufacturer,
                            row.Brand,
                            row.SupplierDescription,
                            row.GlobalDescription,
                            row.ManufacturerPartNumber),
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
                        FirstImageUrl(row.ImageJson),
                        HasCachedInventory(row.WarehouseAvailability),
                        CachedInventoryTotal(row.WarehouseAvailability),
                        false));
            })
            .ToList());
        Timed("build-cross-reference-offers", () =>
        {
            offerCandidates.AddRange(crossReferenceRows.Select(row =>
            {
                crossReferencePrices.TryGetValue(row.SupplierProductId, out var price);
                crossReferenceCompanyPrices.TryGetValue(row.SupplierProductId, out var actualCost);
                return new OfferCandidate(
                    GroupKey(row.NormalizedManufacturerPartNumber, row.ManufacturerPartNumber, row.GlobalProductId, row.SupplierProductId),
                    int.MaxValue,
                    true,
                    new SupplierItemOfferResult(
                        row.SupplierProductId,
                        row.GlobalProductId,
                        row.SupplierCode,
                        row.SupplierName,
                        row.SupplierSku,
                        row.ManufacturerPartNumber,
                        row.Upc,
                        row.Brand,
                        BuildDisplayTitle(
                            row.Manufacturer,
                            row.Brand,
                            row.SupplierDescription,
                            row.GlobalDescription,
                            row.ManufacturerPartNumber),
                        row.Category,
                        row.Status,
                        crossReferenceFitmentRows
                            .Where(x => x.SupplierProductId == row.SupplierProductId || (x.SupplierId == row.SupplierId && x.SupplierSku == row.SupplierSku))
                            .Select(x => x.Id)
                            .Distinct()
                            .Count(),
                        price?.Msrp,
                        price?.DealerCost,
                        crossReferenceCompanyPrices.ContainsKey(row.SupplierProductId) ? actualCost : null,
                        FirstImageUrl(row.ImageJson),
                        HasCachedInventory(row.WarehouseAvailability),
                        CachedInventoryTotal(row.WarehouseAvailability),
                        false));
            }));
            return 0;
        });
        var allFitmentRows = fitmentRows
            .Concat(crossReferenceFitmentRows)
            .ToList();

        var results = Timed("group-build-results", () => offerCandidates
            .GroupBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Min(row => row.SortIndex))
            .Select(group =>
            {
                var orderedOfferCandidates = group
                    .GroupBy(x => x.Offer.SupplierProductId)
                    .Select(x => x.First())
                    .OrderByDescending(x => x.Offer.HasCachedInventory)
                    .ThenBy(x => x.Offer.ActualCost ?? x.Offer.DealerCost ?? decimal.MaxValue)
                    .ThenBy(x => x.IsCrossReference)
                    .ThenBy(x => x.SortIndex)
                    .ThenBy(x => x.Offer.SupplierName)
                    .ThenBy(x => x.Offer.SupplierSku)
                    .ToList();
                var selected = orderedOfferCandidates.First().Offer;
                var offers = orderedOfferCandidates
                    .Select(x => x.Offer)
                    .Select(x => x with { IsDefaultOffer = x.SupplierProductId == selected.SupplierProductId })
                    .ToList();
                selected = offers.First(x => x.IsDefaultOffer);
                var consolidatedFitment = BuildConsolidatedFitment(allFitmentRows, offers);

                return new SupplierItemSearchResult(
                    selected.SupplierProductId,
                    selected.GlobalProductId,
                    selected.SupplierCode,
                    selected.SupplierName,
                    selected.SupplierSku,
                    selected.ManufacturerPartNumber,
                    selected.Upc,
                    selected.Brand,
                    selected.Title,
                    selected.Category,
                    selected.Status,
                    consolidatedFitment.Count,
                    selected.Msrp,
                    selected.DealerCost,
                    selected.ActualCost,
                    selected.ImageUrl ?? offers.FirstOrDefault(x => x.ImageUrl is not null)?.ImageUrl,
                    [],
                    Offers: offers,
                    Fitment: consolidatedFitment);
            })
            .ToList());

        LogSearchTiming("completed", rows.Count, results.Count, crossReferenceRows.Count, allFitmentRows.Count);

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
            offset + rows.Count,
            offset,
            cappedLimit,
            hasMore,
            results,
            isSearchExecuted);

        void LogSearchTiming(string outcome, int rowsCount, int resultCount, int crossReferenceCount, int fitmentCount)
        {
            totalStopwatch.Stop();
            var steps = string.Join(", ", timingSteps.Select(x => $"{x.Name}={x.ElapsedMilliseconds}ms"));
            var slow = totalStopwatch.ElapsedMilliseconds >= 1000 || timingSteps.Any(x => x.ElapsedMilliseconds >= 500);
            if (slow)
            {
                logger.LogWarning(
                    "Supplier item search timing slow. Outcome={Outcome}; TotalMs={TotalMs}; OrganizationScoped={OrganizationScoped}; Query={Query}; Supplier={Supplier}; Ymm={Year}/{Make}/{Model}; Offset={Offset}; Limit={Limit}; Rows={Rows}; Results={Results}; CrossReferences={CrossReferences}; FitmentRows={FitmentRows}; Steps={Steps}",
                    outcome,
                    totalStopwatch.ElapsedMilliseconds,
                    organizationId is not null,
                    cleanQuery,
                    cleanSupplierCode,
                    selectedYear,
                    cleanMake,
                    cleanModel,
                    offset,
                    cappedLimit,
                    rowsCount,
                    resultCount,
                    crossReferenceCount,
                    fitmentCount,
                    steps);
                return;
            }

            logger.LogInformation(
                "Supplier item search timing. Outcome={Outcome}; TotalMs={TotalMs}; OrganizationScoped={OrganizationScoped}; Query={Query}; Supplier={Supplier}; Ymm={Year}/{Make}/{Model}; Offset={Offset}; Limit={Limit}; Rows={Rows}; Results={Results}; CrossReferences={CrossReferences}; FitmentRows={FitmentRows}; Steps={Steps}",
                outcome,
                totalStopwatch.ElapsedMilliseconds,
                organizationId is not null,
                cleanQuery,
                cleanSupplierCode,
                selectedYear,
                cleanMake,
                cleanModel,
                offset,
                cappedLimit,
                rowsCount,
                resultCount,
                crossReferenceCount,
                fitmentCount,
                steps);
        }
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

    private static string GroupKey(string? normalizedManufacturerPartNumber, string? manufacturerPartNumber, Guid globalProductId, Guid supplierProductId)
    {
        var normalized = Clean(normalizedManufacturerPartNumber) ?? ProductMatchingService.NormalizeManufacturerPartNumber(manufacturerPartNumber);
        if (normalized is not null)
        {
            return $"mfg:{normalized}";
        }

        return globalProductId == Guid.Empty
            ? $"supplier:{supplierProductId:N}"
            : $"global:{globalProductId:N}";
    }

    private static bool HasCachedInventory(string? warehouseAvailability)
    {
        if (string.IsNullOrWhiteSpace(warehouseAvailability))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(warehouseAvailability);
            return JsonHasInventory(document.RootElement);
        }
        catch (JsonException)
        {
            return warehouseAvailability.Contains("in stock", StringComparison.OrdinalIgnoreCase) ||
                warehouseAvailability.Contains("available", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int? CachedInventoryTotal(string? warehouseAvailability)
    {
        if (string.IsNullOrWhiteSpace(warehouseAvailability))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(warehouseAvailability);
            var total = JsonInventoryTotal(document.RootElement);
            return total > 0 ? total : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int JsonInventoryTotal(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var total = 0;
                foreach (var property in element.EnumerateObject())
                {
                    if (IsQuantityProperty(property.Name) &&
                        property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var quantity) &&
                        quantity > 0)
                    {
                        total += quantity;
                        continue;
                    }

                    total += JsonInventoryTotal(property.Value);
                }

                return total;
            case JsonValueKind.Array:
                return element.EnumerateArray().Sum(JsonInventoryTotal);
            default:
                return 0;
        }
    }

    private static bool IsQuantityProperty(string name)
    {
        return name.Equals("quantity", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("qty", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("available_quantity", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("availableQuantity", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("inventory", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("stock", StringComparison.OrdinalIgnoreCase);
    }

    private static bool JsonHasInventory(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Equals("quantity", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("available", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("qty", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("stock", StringComparison.OrdinalIgnoreCase))
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number &&
                            property.Value.TryGetInt32(out var quantity) &&
                            quantity > 0)
                        {
                            return true;
                        }

                        if (property.Value.ValueKind == JsonValueKind.True)
                        {
                            return true;
                        }
                    }

                    if (JsonHasInventory(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(JsonHasInventory);
            case JsonValueKind.Number:
                return element.TryGetInt32(out var value) && value > 0;
            case JsonValueKind.True:
                return true;
            default:
                return false;
        }
    }

    private static IReadOnlyCollection<SupplierItemFitmentResult> BuildConsolidatedFitment(
        IReadOnlyCollection<FitmentSourceRow> fitmentRows,
        IReadOnlyCollection<SupplierItemOfferResult> offers)
    {
        if (fitmentRows.Count == 0 || offers.Count == 0)
        {
            return [];
        }

        var offerProductIds = offers
            .Select(x => x.SupplierProductId)
            .ToHashSet();
        var offerKeys = offers
            .Select(x => $"{x.SupplierCode}|{x.SupplierSku}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return fitmentRows
            .Where(row =>
                (row.SupplierProductId is not null && offerProductIds.Contains(row.SupplierProductId.Value)) ||
                offerKeys.Contains($"{row.SupplierKey}|{row.SupplierSku}"))
            .GroupBy(row => new
            {
                row.Year,
                Make = Clean(row.Make) ?? string.Empty,
                Model = Clean(row.Model) ?? string.Empty,
                Submodel = Clean(row.Submodel),
                Engine = Clean(row.Engine),
                Notes = Clean(row.Notes),
                VehicleType = Clean(row.VehicleType)
            })
            .Select(group => new SupplierItemFitmentResult(
                group.Key.Year,
                group.Key.Make,
                group.Key.Model,
                group.Key.Submodel,
                group.Key.Engine,
                group.Key.Notes,
                group.Key.VehicleType,
                group.Select(x => Clean(x.SupplierKey) ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList(),
                group.Select(x => Clean(x.SupplierSku) ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList()))
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.Make)
            .ThenBy(x => x.Model)
            .ThenBy(x => x.Submodel)
            .ThenBy(x => x.Engine)
            .ToList();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsLikelyCatalogIdentifier(string? query)
    {
        var clean = Clean(query);
        if (clean is null || !clean.Any(char.IsDigit))
        {
            return false;
        }

        var tokens = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 2)
        {
            return false;
        }

        if (tokens.Length == 2 && tokens[0].Length > 3)
        {
            return false;
        }

        return clean.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '-' or '_' or '.' or '/' or '\\' or ' ');
    }

    private sealed record OfferCandidate(
        string GroupKey,
        int SortIndex,
        bool IsCrossReference,
        SupplierItemOfferResult Offer);

    private sealed record SearchTimingStep(string Name, long ElapsedMilliseconds);

    private sealed record FitmentSourceRow(
        Guid Id,
        Guid? SupplierProductId,
        Guid SupplierId,
        string SupplierKey,
        string SupplierSku,
        int Year,
        string Make,
        string Model,
        string? Submodel,
        string? Engine,
        string? Notes,
        string? VehicleType);

    private sealed record CrossReferenceRow(
        string? NormalizedManufacturerPartNumber,
        Guid SupplierProductId,
        Guid SupplierId,
        Guid GlobalProductId,
        string SupplierCode,
        string SupplierName,
        string SupplierSku,
        string? ManufacturerPartNumber,
        string? Upc,
        string Brand,
        string? Manufacturer,
        string? SupplierDescription,
        string GlobalDescription,
        string? Category,
        string? WarehouseAvailability,
        string? ImageJson,
        string Status);

    private static string BuildDisplayTitle(
        string? manufacturer,
        string brand,
        string? supplierDescription,
        string globalDescription,
        string? manufacturerPartNumber)
    {
        var title = Clean(supplierDescription) ?? Clean(globalDescription) ?? Clean(manufacturerPartNumber) ?? "Supplier item";
        var maker = Clean(manufacturer) ?? Clean(brand);
        if (!string.IsNullOrWhiteSpace(maker) &&
            !title.StartsWith($"{maker} ", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(title, maker, StringComparison.OrdinalIgnoreCase))
        {
            title = $"{maker} {title}";
        }

        var partNumber = Clean(manufacturerPartNumber);
        if (!string.IsNullOrWhiteSpace(partNumber) &&
            !title.Contains(partNumber, StringComparison.OrdinalIgnoreCase))
        {
            title = $"{title} - {partNumber}";
        }

        return title;
    }
}
