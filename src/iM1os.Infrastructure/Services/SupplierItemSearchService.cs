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
    private static readonly IReadOnlyDictionary<string, string> MakerAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ALLBALLSRACING"] = "ALLBALLS",
        ["MAXIMARACINGOIL"] = "MAXIMA"
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SupplierWarehouseNames =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["WPS"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CA"] = "CA",
                ["GA"] = "GA",
                ["ID"] = "ID",
                ["IN"] = "IN",
                ["PA"] = "PA",
                ["PA2"] = "PA2",
                ["TX"] = "TX"
            },
            ["TURN14"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["01"] = "Turn14 East",
                ["02"] = "Turn14 West",
                ["03"] = "Turn14 Midwest",
                ["59"] = "Turn14 Central"
            },
            ["PU"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NC"] = "NC",
                ["NV"] = "NV",
                ["NY"] = "NY",
                ["TX"] = "TX",
                ["WI"] = "WI"
            }
        };

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
        var cleanCategory = Clean(request.Category);
        if (!IsUsableCatalogCategory(cleanCategory))
        {
            cleanCategory = null;
        }
        var cleanBrand = Clean(request.Brand);
        var cleanTireBrand = Clean(request.TireBrand);
        var cleanTireModelLine = Clean(request.TireModelLine)?.ToUpperInvariant();
        var cleanTirePosition = Clean(request.TirePosition)?.ToLowerInvariant();
        var selectedYear = request.Year;
        var normalizedPartQuery = ProductMatchingService.NormalizeManufacturerPartNumber(cleanQuery);
        var lowerQuery = cleanQuery?.ToLowerInvariant();
        var likeQuery = cleanQuery is null ? null : $"%{cleanQuery}%";
        var prefixLikeQuery = cleanQuery is null ? null : $"{cleanQuery}%";
        var searchTokens = SearchTokens(cleanQuery);
        var numericSearchTokens = searchTokens.Where(IsNumericSearchToken).Take(3).ToArray();
        var textSearchTokens = searchTokens.Where(x => !IsNumericSearchToken(x)).ToArray();
        var textSearchTokenGroups = textSearchTokens.Select(SingularPluralVariants).ToArray();
        var searchTokenGroups = searchTokens
            .Select(token => IsNumericSearchToken(token) ? [token] : SingularPluralVariants(token))
            .ToArray();
        var textSearchTsQuery = textSearchTokenGroups.Length == 0 ? null : BuildPrefixTsQuery(textSearchTokenGroups);
        var searchToken0a = TokenVariantAt(searchTokenGroups, 0, 0);
        var searchToken0b = TokenVariantAt(searchTokenGroups, 0, 1);
        var searchToken1a = TokenVariantAt(searchTokenGroups, 1, 0);
        var searchToken1b = TokenVariantAt(searchTokenGroups, 1, 1);
        var searchToken2a = TokenVariantAt(searchTokenGroups, 2, 0);
        var searchToken2b = TokenVariantAt(searchTokenGroups, 2, 1);
        var searchToken3a = TokenVariantAt(searchTokenGroups, 3, 0);
        var searchToken3b = TokenVariantAt(searchTokenGroups, 3, 1);
        var searchToken4a = TokenVariantAt(searchTokenGroups, 4, 0);
        var searchToken4b = TokenVariantAt(searchTokenGroups, 4, 1);
        var numericLike0 = numericSearchTokens.ElementAtOrDefault(0) is { } numericToken0 ? $"%{numericToken0}%" : null;
        var numericLike1 = numericSearchTokens.ElementAtOrDefault(1) is { } numericToken1 ? $"%{numericToken1}%" : null;
        var numericLike2 = numericSearchTokens.ElementAtOrDefault(2) is { } numericToken2 ? $"%{numericToken2}%" : null;
        var cappedLimit = Math.Clamp(limit, 1, 100);
        var offset = Math.Max(0, request.Offset);
        var hasTextSearch = cleanQuery is not null;
        var isIdentifierSearch = IsLikelyCatalogIdentifier(cleanQuery);
        var hasYmmSearch = selectedYear is not null && cleanMake is not null && cleanModel is not null;
        var hasTireSearch = cleanTireBrand is not null ||
            cleanTireModelLine is not null ||
            request.TireWidth is not null ||
            request.TireAspectRatio is not null ||
            request.TireRimDiameter is not null ||
            cleanTirePosition is not null;
        var hasSupplierFilter = cleanSupplierCode is not null;
        var hasCategoryFilter = cleanCategory is not null;
        var hasBrandFilter = cleanBrand is not null;
        var isSearchExecuted = request.SearchExecuted || hasTextSearch || hasYmmSearch || hasTireSearch || hasSupplierFilter || hasCategoryFilter || hasBrandFilter || offset > 0;
        var hasSearchCriteria = hasTextSearch || hasYmmSearch || hasTireSearch || hasSupplierFilter || hasCategoryFilter || hasBrandFilter;
        var includeFacets = request.IncludeFacets;
        var includeYmmFacets = includeFacets && !hasTextSearch;
        var usePostgresLike = dbContext is DbContext efDbContext &&
            efDbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        var enabledCompanySupplierCodes = organizationId is null
            ? null
            : await TimedAsync("company-supplier-entitlements", () => tenantModuleEntitlements.GetEnabledSupplierConnectorCodesAsync(organizationId.Value, cancellationToken));
        var enabledCompanySupplierCodeList = enabledCompanySupplierCodes?.ToArray() ?? [];
        var supplierPreferences = organizationId is null
            ? SupplierPurchasePreferences.Empty
            : await TimedAsync("company-supplier-preferences", () => GetSupplierPurchasePreferencesAsync(organizationId.Value, cancellationToken));
        var facetScopeCacheKey = organizationId is null
            ? "platform"
            : $"company:{organizationId.Value:N}:{string.Join("-", enabledCompanySupplierCodeList.Order(StringComparer.OrdinalIgnoreCase))}";
        var facetCachePrefix = $"supplier-item-search:ymm-facets:v4:{facetScopeCacheKey}:supplier:{cleanSupplierCode ?? "*"}";
        var requestedDisabledCompanySupplier = organizationId is not null &&
            cleanSupplierCode is not null &&
            enabledCompanySupplierCodes is not null &&
            !enabledCompanySupplierCodes.Contains(cleanSupplierCode);
        IReadOnlyCollection<SupplierSearchOption> availableSuppliers = [];
        IReadOnlyCollection<SupplierSearchOption> configuredSuppliers = [];
        IReadOnlyCollection<string> availableCategories = [];
        IReadOnlyCollection<string> availableBrands = [];
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
            availableCategories = await TimedCachedFacetAsync("facets-categories", $"{facetCachePrefix}:categories:brand:{cleanBrand ?? "*"}", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                        on supplierProduct.GlobalProductId equals globalProduct.Id
                    where
                        globalProduct.Category != null &&
                        globalProduct.Category != string.Empty &&
                        globalProduct.Category.Length > 1 &&
                        (cleanBrand == null || globalProduct.Brand == cleanBrand) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select globalProduct.Category!)
                .Distinct()
                .OrderBy(x => x)
                .Take(300)
                .ToListAsync(cancellationToken));
            availableBrands = await TimedCachedFacetAsync("facets-brands", $"{facetCachePrefix}:brands:category:{cleanCategory ?? "*"}", () => (
                    from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on supplierProduct.SupplierId equals supplier.Id
                    join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                        on supplierProduct.GlobalProductId equals globalProduct.Id
                    where
                        globalProduct.Brand != string.Empty &&
                        (cleanCategory == null || globalProduct.Category == cleanCategory) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                        (!hasSupplierFilter || supplier.Code == cleanSupplierCode)
                    select globalProduct.Brand)
                .Distinct()
                .OrderBy(x => x)
                .Take(1200)
                .ToListAsync(cancellationToken));
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
                cleanCategory,
                cleanBrand,
                cleanTireBrand,
                cleanTireModelLine,
                request.TireWidth,
                request.TireAspectRatio,
                request.TireRimDiameter,
                cleanTirePosition,
                availableSuppliers,
                configuredSuppliers,
                availableCategories,
                availableBrands,
                availableVehicleTypes,
                availableYears,
                availableMakes,
                availableModels,
                0,
                offset,
                cappedLimit,
                false,
                [],
                isSearchExecuted,
                request.UseNormalizedCatalog,
                totalStopwatch.ElapsedMilliseconds);
        }

        if (request.UseNormalizedCatalog)
        {
            var normalizedFastPathCanonicalItemIds = Array.Empty<Guid>();
            if (hasTextSearch && isIdentifierSearch)
            {
                var fastPathCanonicalItemIds = await TimedAsync("normalized-fast-canonical-identifiers", async () =>
                {
                    var canonicalItemIds = await dbContext.CanonicalItems
                        .AsNoTracking()
                        .Where(canonicalItem =>
                            (normalizedPartQuery != null &&
                                canonicalItem.NormalizedManufacturerPartNumber != null &&
                                canonicalItem.NormalizedManufacturerPartNumber == normalizedPartQuery) ||
                            (cleanQuery != null &&
                                canonicalItem.PrimaryUpc != null &&
                                canonicalItem.PrimaryUpc == cleanQuery) ||
                            (cleanQuery != null &&
                                canonicalItem.ManufacturerPartNumber != null &&
                                (usePostgresLike
                                    ? EF.Functions.ILike(canonicalItem.ManufacturerPartNumber, cleanQuery)
                                    : canonicalItem.ManufacturerPartNumber.ToLower() == lowerQuery)))
                        .Select(canonicalItem => canonicalItem.Id)
                        .Take(1000)
                        .ToListAsync(cancellationToken);

                    var identifierCanonicalItemIds = await dbContext.CanonicalItemIdentifiers
                        .AsNoTracking()
                        .Where(identifier =>
                            (normalizedPartQuery != null && identifier.NormalizedValue == normalizedPartQuery) ||
                            (cleanQuery != null &&
                                (usePostgresLike
                                    ? EF.Functions.ILike(identifier.IdentifierValue, cleanQuery)
                                    : identifier.IdentifierValue.ToLower() == lowerQuery)))
                        .Select(identifier => identifier.CanonicalItemId)
                        .Take(1000)
                        .ToListAsync(cancellationToken);

                    var offerCanonicalItemIds = await dbContext.CanonicalItemSupplierOffers
                        .AsNoTracking()
                        .Where(offer =>
                            cleanQuery != null &&
                            ((usePostgresLike
                                ? EF.Functions.ILike(offer.SupplierSku, cleanQuery)
                                : offer.SupplierSku.ToLower() == lowerQuery) ||
                            (offer.SupplierPartNumber != null &&
                                (usePostgresLike
                                    ? EF.Functions.ILike(offer.SupplierPartNumber, cleanQuery)
                                    : offer.SupplierPartNumber.ToLower() == lowerQuery))))
                        .Select(offer => offer.CanonicalItemId)
                        .Take(1000)
                        .ToListAsync(cancellationToken);

                    return canonicalItemIds
                        .Concat(identifierCanonicalItemIds)
                        .Concat(offerCanonicalItemIds)
                        .Distinct()
                        .Take(1000)
                        .ToArray();
                });

                normalizedFastPathCanonicalItemIds = fastPathCanonicalItemIds;
            }

            var normalizedYmmCanonicalItemIds = Array.Empty<Guid>();
            if (hasYmmSearch)
            {
                var canonicalYmmCandidateIds = await TimedAsync("normalized-ymm-canonical-fitment-candidates", () => dbContext.CanonicalFitments
                    .AsNoTracking()
                    .Where(fitment =>
                        fitment.Year == selectedYear!.Value &&
                        fitment.Make == cleanMake &&
                        fitment.Model == cleanModel &&
                        (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType))
                    .Select(fitment => fitment.CanonicalItemId)
                    .Distinct()
                    .Take(50000)
                    .ToListAsync(cancellationToken));
                var directYmmCandidateIds = await TimedAsync("normalized-ymm-direct-fitment-candidates", () => (
                        from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                        join offer in dbContext.CanonicalItemSupplierOffers.AsNoTracking()
                            on fitment.SupplierProductId equals (Guid?)offer.SupplierProductId
                        where
                            fitment.SupplierProductId != null &&
                            fitment.Year == selectedYear!.Value &&
                            fitment.Make == cleanMake &&
                            fitment.Model == cleanModel &&
                            (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                            (organizationId == null || enabledCompanySupplierCodeList.Contains(offer.SupplierCode)) &&
                            (!hasSupplierFilter || offer.SupplierCode == cleanSupplierCode)
                        select offer.CanonicalItemId)
                    .Distinct()
                    .Take(50000)
                    .ToListAsync(cancellationToken));
                var skuYmmCandidateIds = await TimedAsync("normalized-ymm-sku-fitment-candidates", () => (
                        from fitment in dbContext.SupplierFitmentRecords.AsNoTracking()
                        join offer in dbContext.CanonicalItemSupplierOffers.AsNoTracking()
                            on new { fitment.SupplierId, fitment.SupplierSku } equals new { offer.SupplierId, offer.SupplierSku }
                        where
                            fitment.Year == selectedYear!.Value &&
                            fitment.Make == cleanMake &&
                            fitment.Model == cleanModel &&
                            (cleanVehicleType == null || fitment.VehicleType == cleanVehicleType) &&
                            (organizationId == null || enabledCompanySupplierCodeList.Contains(offer.SupplierCode)) &&
                            (!hasSupplierFilter || offer.SupplierCode == cleanSupplierCode)
                        select offer.CanonicalItemId)
                    .Distinct()
                    .Take(50000)
                    .ToListAsync(cancellationToken));

                normalizedYmmCanonicalItemIds = canonicalYmmCandidateIds
                    .Concat(directYmmCandidateIds)
                    .Concat(skuYmmCandidateIds)
                    .Distinct()
                    .ToArray();
            }

            var hasNormalizedFastPathMatches = normalizedFastPathCanonicalItemIds.Length > 0;
            var normalizedRows = await TimedAsync(hasNormalizedFastPathMatches ? "normalized-canonical-fast-query" : "normalized-canonical-query", () => (
                    from canonicalItem in dbContext.CanonicalItems.AsNoTracking()
                    where
                        (!hasNormalizedFastPathMatches || normalizedFastPathCanonicalItemIds.Contains(canonicalItem.Id)) &&
                        (cleanCategory == null || canonicalItem.Category == cleanCategory) &&
                        (cleanBrand == null || canonicalItem.Brand == cleanBrand) &&
                        (!hasSupplierFilter || dbContext.CanonicalItemSupplierOffers.AsNoTracking().Any(offer =>
                            offer.CanonicalItemId == canonicalItem.Id &&
                            offer.SupplierCode == cleanSupplierCode)) &&
                        (organizationId == null || dbContext.CanonicalItemSupplierOffers.AsNoTracking().Any(offer =>
                            offer.CanonicalItemId == canonicalItem.Id &&
                            enabledCompanySupplierCodeList.Contains(offer.SupplierCode))) &&
                        (!hasYmmSearch || normalizedYmmCanonicalItemIds.Contains(canonicalItem.Id)) &&
                        (!hasTireSearch ||
                            ((cleanTireBrand == null || (canonicalItem.Brand != null && (usePostgresLike ? EF.Functions.ILike(canonicalItem.Brand, cleanTireBrand) : canonicalItem.Brand.ToLower() == cleanTireBrand.ToLower()))) &&
                            dbContext.CanonicalItemSources.AsNoTracking().Any(source =>
                                source.CanonicalItemId == canonicalItem.Id &&
                                source.GlobalProductId != null &&
                                dbContext.GlobalProducts.AsNoTracking().Any(globalProduct =>
                                    globalProduct.Id == source.GlobalProductId.Value &&
                                    (cleanTireModelLine == null || globalProduct.TireModelLine == cleanTireModelLine) &&
                                    (request.TireWidth == null || globalProduct.TireWidth == request.TireWidth) &&
                                    (request.TireAspectRatio == null || globalProduct.TireAspectRatio == request.TireAspectRatio) &&
                                    (request.TireRimDiameter == null || globalProduct.TireRimDiameter == request.TireRimDiameter) &&
                                    (cleanTirePosition == null || globalProduct.TirePosition == cleanTirePosition))))) &&
                        (!hasTextSearch ||
                            hasNormalizedFastPathMatches ||
                            (canonicalItem.Title.ToLower() == lowerQuery) ||
                            (canonicalItem.ManufacturerPartNumber != null && canonicalItem.ManufacturerPartNumber.ToLower() == lowerQuery) ||
                            (canonicalItem.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && canonicalItem.NormalizedManufacturerPartNumber == normalizedPartQuery) ||
                            (canonicalItem.PrimaryUpc != null && canonicalItem.PrimaryUpc == cleanQuery) ||
                            (usePostgresLike
                                ? ((canonicalItem.SearchText != null && EF.Functions.ILike(canonicalItem.SearchText, likeQuery!)) ||
                                    EF.Functions.ILike(canonicalItem.Title, likeQuery!) ||
                                    (canonicalItem.Brand != null && EF.Functions.ILike(canonicalItem.Brand, likeQuery!)) ||
                                    (canonicalItem.Manufacturer != null && EF.Functions.ILike(canonicalItem.Manufacturer, likeQuery!)) ||
                                    (canonicalItem.ManufacturerPartNumber != null && EF.Functions.ILike(canonicalItem.ManufacturerPartNumber, likeQuery!)))
                                : ((canonicalItem.SearchText != null && canonicalItem.SearchText.ToLower().Contains(lowerQuery!)) ||
                                    canonicalItem.Title.ToLower().Contains(lowerQuery!) ||
                                    (canonicalItem.Brand != null && canonicalItem.Brand.ToLower().Contains(lowerQuery!)) ||
                                    (canonicalItem.Manufacturer != null && canonicalItem.Manufacturer.ToLower().Contains(lowerQuery!)) ||
                                    (canonicalItem.ManufacturerPartNumber != null && canonicalItem.ManufacturerPartNumber.ToLower().Contains(lowerQuery!)))) ||
                            dbContext.CanonicalItemIdentifiers.AsNoTracking().Any(identifier =>
                                identifier.CanonicalItemId == canonicalItem.Id &&
                                ((usePostgresLike ? EF.Functions.ILike(identifier.IdentifierValue, likeQuery!) : identifier.IdentifierValue.ToLower().Contains(lowerQuery!)) ||
                                    (usePostgresLike ? EF.Functions.ILike(identifier.NormalizedValue, likeQuery!) : identifier.NormalizedValue.ToLower().Contains(lowerQuery!)))) ||
                            dbContext.CanonicalItemSupplierOffers.AsNoTracking().Any(offer =>
                                offer.CanonicalItemId == canonicalItem.Id &&
                                ((usePostgresLike ? EF.Functions.ILike(offer.SupplierSku, likeQuery!) : offer.SupplierSku.ToLower().Contains(lowerQuery!)) ||
                                    (offer.SupplierPartNumber != null && (usePostgresLike ? EF.Functions.ILike(offer.SupplierPartNumber, likeQuery!) : offer.SupplierPartNumber.ToLower().Contains(lowerQuery!))) ||
                                    (offer.SupplierTitle != null && (usePostgresLike ? EF.Functions.ILike(offer.SupplierTitle, likeQuery!) : offer.SupplierTitle.ToLower().Contains(lowerQuery!))))))
                    orderby
                        !hasTextSearch ? 100 :
                            canonicalItem.ManufacturerPartNumber != null && canonicalItem.ManufacturerPartNumber.ToLower() == lowerQuery ? 0 :
                            canonicalItem.NormalizedManufacturerPartNumber != null && normalizedPartQuery != null && canonicalItem.NormalizedManufacturerPartNumber == normalizedPartQuery ? 0 :
                            canonicalItem.PrimaryUpc != null && canonicalItem.PrimaryUpc == cleanQuery ? 0 :
                            canonicalItem.Title.ToLower() == lowerQuery ? 1 :
                            canonicalItem.Title.ToLower().StartsWith(lowerQuery!) ? 3 :
                            10,
                        canonicalItem.Brand,
                        canonicalItem.ManufacturerPartNumber,
                        canonicalItem.Title
                    select new NormalizedCanonicalRow(
                        canonicalItem.Id,
                        canonicalItem.Brand,
                        canonicalItem.Manufacturer,
                        canonicalItem.ManufacturerPartNumber,
                        canonicalItem.NormalizedManufacturerPartNumber,
                        canonicalItem.PrimaryUpc,
                        canonicalItem.Title,
                        canonicalItem.Category,
                        canonicalItem.PrimaryImageUrl,
                        canonicalItem.SearchText,
                        canonicalItem.Status))
                .Skip(offset)
                .Take(cappedLimit + 1)
                .ToListAsync(cancellationToken));
            var normalizedHasMore = normalizedRows.Count > cappedLimit;
            if (normalizedHasMore)
            {
                normalizedRows = normalizedRows.Take(cappedLimit).ToList();
            }

            var canonicalItemIds = normalizedRows.Select(x => x.CanonicalItemId).ToArray();
            var normalizedOfferRows = canonicalItemIds.Length == 0
                ? []
                : await TimedAsync("normalized-offers", () => (
                    from offer in dbContext.CanonicalItemSupplierOffers.AsNoTracking()
                    join supplier in dbContext.Suppliers.AsNoTracking()
                        on offer.SupplierId equals supplier.Id
                    join supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                        on offer.SupplierProductId equals supplierProduct.Id
                    join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                        on supplierProduct.GlobalProductId equals globalProduct.Id
                    where canonicalItemIds.Contains(offer.CanonicalItemId) &&
                        (organizationId == null || enabledCompanySupplierCodeList.Contains(offer.SupplierCode)) &&
                        (!hasSupplierFilter || offer.SupplierCode == cleanSupplierCode)
                    select new NormalizedOfferRow(
                        offer.CanonicalItemId,
                        offer.SupplierProductId,
                        supplierProduct.GlobalProductId,
                        offer.SupplierId,
                        offer.SupplierCode,
                        supplier.Name,
                        offer.SupplierSku,
                        supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber ?? offer.SupplierPartNumber,
                        supplierProduct.NormalizedManufacturerPartNumber ?? globalProduct.NormalizedManufacturerPartNumber,
                        globalProduct.Upc,
                        offer.SupplierTitle,
                        globalProduct.LongDescription,
                        globalProduct.SpecificationsJson,
                        offer.Status,
                        offer.ListPrice,
                        offer.DealerCost,
                        supplierProduct.CaseQuantity,
                        offer.WarehouseAvailability,
                        offer.ImageUrl,
                        supplierProduct.SupplierImagesJson,
                        globalProduct.ImagesJson))
                    .ToListAsync(cancellationToken));
            var normalizedSupplierProductIds = normalizedOfferRows.Select(x => x.SupplierProductId).Distinct().ToArray();
            var normalizedCompanyPrices = organizationId is null || normalizedSupplierProductIds.Length == 0
                ? []
                : await TimedAsync("normalized-company-prices", () => dbContext.CompanySupplierPrices
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId.Value && normalizedSupplierProductIds.Contains(x.SupplierProductId))
                    .ToDictionaryAsync(x => x.SupplierProductId, x => x.ActualDealerCost, cancellationToken));
            var normalizedFitmentRows = canonicalItemIds.Length == 0
                ? new List<NormalizedFitmentRow>()
                : await TimedAsync("normalized-fitment", () => dbContext.CanonicalFitments
                    .AsNoTracking()
                    .Where(x => canonicalItemIds.Contains(x.CanonicalItemId))
                    .Select(x => new NormalizedFitmentRow(
                        x.CanonicalItemId,
                        x.Year,
                        x.Make,
                        x.Model,
                        x.Submodel,
                        x.Engine,
                        x.Notes,
                        x.VehicleType))
                    .ToListAsync(cancellationToken));
            var normalizedSupplierIds = normalizedOfferRows.Select(x => x.SupplierId).Distinct().ToArray();
            var normalizedSupplierSkus = normalizedOfferRows.Select(x => x.SupplierSku).Distinct().ToArray();
            var normalizedSourceFitmentRows = normalizedSupplierProductIds.Length == 0
                ? []
                : await TimedAsync("normalized-source-fitment", () => dbContext.SupplierFitmentRecords
                    .AsNoTracking()
                    .Where(x =>
                        (x.SupplierProductId != null && normalizedSupplierProductIds.Contains(x.SupplierProductId.Value)) ||
                        (normalizedSupplierIds.Contains(x.SupplierId) && normalizedSupplierSkus.Contains(x.SupplierSku)))
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
            if (normalizedSourceFitmentRows.Count > 0)
            {
                var canonicalBySupplierProductId = normalizedOfferRows
                    .GroupBy(x => x.SupplierProductId)
                    .ToDictionary(x => x.Key, x => x.First().CanonicalItemId);
                var canonicalBySupplierKey = normalizedOfferRows
                    .GroupBy(x => $"{x.SupplierId:N}|{x.SupplierSku}", StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().CanonicalItemId, StringComparer.OrdinalIgnoreCase);

                normalizedFitmentRows.AddRange(normalizedSourceFitmentRows.Select(row =>
                {
                    Guid? canonicalItemId = null;
                    if (row.SupplierProductId is Guid supplierProductId &&
                        canonicalBySupplierProductId.TryGetValue(supplierProductId, out var productCanonicalItemId))
                    {
                        canonicalItemId = productCanonicalItemId;
                    }
                    else if (canonicalBySupplierKey.TryGetValue($"{row.SupplierId:N}|{row.SupplierSku}", out var supplierKeyCanonicalItemId))
                    {
                        canonicalItemId = supplierKeyCanonicalItemId;
                    }

                    return canonicalItemId is null
                        ? null
                        : new NormalizedFitmentRow(
                            canonicalItemId.Value,
                            row.Year,
                            row.Make,
                            row.Model,
                            row.Submodel,
                            row.Engine,
                            row.Notes,
                            row.VehicleType);
                }).Where(x => x is not null).Select(x => x!));
            }

            var normalizedRowsById = normalizedRows.ToDictionary(x => x.CanonicalItemId);
            var normalizedResults = Timed("normalized-build-results", () => normalizedRows
                .Select((row, index) => new
                {
                    Row = row,
                    SortIndex = index,
                    GroupKey = GroupKey(row.Brand, row.Manufacturer, row.NormalizedManufacturerPartNumber, row.ManufacturerPartNumber, Guid.Empty, row.CanonicalItemId)
                })
                .GroupBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Min(x => x.SortIndex))
                .Select(group =>
                {
                    var groupRows = group.Select(x => x.Row).ToList();
                    var groupCanonicalItemIds = groupRows.Select(x => x.CanonicalItemId).ToHashSet();
                    var offerRows = normalizedOfferRows
                        .Where(x => groupCanonicalItemIds.Contains(x.CanonicalItemId))
                        .OrderBy(x => ServerPreferenceRank(x.SupplierCode, x.WarehouseAvailability, supplierPreferences))
                        .ThenBy(x => normalizedCompanyPrices.TryGetValue(x.SupplierProductId, out var actualCost) ? actualCost : x.DealerCost ?? decimal.MaxValue)
                        .ThenBy(x => x.SupplierName)
                        .ThenBy(x => x.SupplierSku)
                        .ToList();
                    if (offerRows.Count == 0)
                    {
                        return null;
                    }

                    var selectedRow = offerRows.First();
                    var displayRow = normalizedRowsById.TryGetValue(selectedRow.CanonicalItemId, out var selectedCanonicalRow)
                        ? selectedCanonicalRow
                        : groupRows.First();
                    var offers = offerRows
                        .Select(offer =>
                        {
                            var offerRow = normalizedRowsById.TryGetValue(offer.CanonicalItemId, out var canonicalRow)
                                ? canonicalRow
                                : displayRow;
                            normalizedCompanyPrices.TryGetValue(offer.SupplierProductId, out var actualCost);
                            return new SupplierItemOfferResult(
                                offer.SupplierProductId,
                                offer.GlobalProductId,
                                offer.SupplierCode,
                                offer.SupplierName,
                                offer.SupplierSku,
                                offer.ManufacturerPartNumber,
                                offer.Upc,
                                displayRow.Brand ?? offerRow.Brand ?? string.Empty,
                                BuildDisplayTitle(
                                    offer.SupplierCode,
                                    displayRow.Manufacturer ?? offerRow.Manufacturer,
                                    displayRow.Brand ?? offerRow.Brand ?? string.Empty,
                                    offer.SupplierTitle,
                                    offerRow.Title,
                                    offer.LongDescription,
                                    offer.ManufacturerPartNumber),
                                displayRow.Category ?? offerRow.Category,
                                offer.LongDescription,
                                ProductFeatures(offer.SpecificationsJson),
                                offer.Status,
                                normalizedFitmentRows.Count(x => groupCanonicalItemIds.Contains(x.CanonicalItemId)),
                                offer.Msrp,
                                offer.DealerCost,
                                normalizedCompanyPrices.ContainsKey(offer.SupplierProductId) ? actualCost : null,
                                offer.CaseQuantity,
                                FirstImageUrl(offer.ImageUrl) ?? FirstImageUrl(offer.SupplierImageJson) ?? FirstImageUrl(offer.GlobalImageJson),
                                HasCachedInventory(offer.WarehouseAvailability),
                                CachedInventoryTotal(offer.WarehouseAvailability),
                                supplierPreferences.IsPreferredSupplier(offer.SupplierCode),
                                supplierPreferences.PreferredWarehouseCode(offer.SupplierCode),
                                supplierPreferences.PreferredWarehouseName(offer.SupplierCode),
                                offer.SupplierProductId == selectedRow.SupplierProductId);
                        })
                        .ToList();
                    var selected = offers.First(x => x.IsDefaultOffer);
                    var fitment = normalizedFitmentRows
                        .Where(x => groupCanonicalItemIds.Contains(x.CanonicalItemId))
                        .GroupBy(x => new { x.Year, x.Make, x.Model, x.Submodel, x.Engine, x.Notes, x.VehicleType })
                        .Select(group => new SupplierItemFitmentResult(
                            group.Key.Year,
                            group.Key.Make,
                            group.Key.Model,
                            group.Key.Submodel,
                            group.Key.Engine,
                            group.Key.Notes,
                            group.Key.VehicleType,
                            offers.Select(x => x.SupplierCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                            offers.Select(x => x.SupplierSku).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList()))
                        .OrderByDescending(x => x.Year)
                        .ThenBy(x => x.Make)
                        .ThenBy(x => x.Model)
                        .ToList();

                    return new SupplierItemSearchResult(
                        selected.SupplierProductId,
                        selected.GlobalProductId,
                        selected.SupplierCode,
                        selected.SupplierName,
                        selected.SupplierSku,
                        selected.ManufacturerPartNumber ?? displayRow.ManufacturerPartNumber,
                        selected.Upc ?? displayRow.PrimaryUpc,
                        displayRow.Brand ?? string.Empty,
                        displayRow.Title,
                        displayRow.Category,
                        selected.LongDescription,
                        selected.ProductFeatures,
                        displayRow.Status,
                        fitment.Count,
                        selected.Msrp,
                        selected.DealerCost,
                        selected.ActualCost,
                        selected.ImageUrl ?? displayRow.PrimaryImageUrl ?? offers.FirstOrDefault(x => x.ImageUrl is not null)?.ImageUrl,
                        [],
                        Offers: offers,
                        Fitment: fitment);
                })
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList());

            LogSearchTiming("normalized-completed", normalizedRows.Count, normalizedResults.Count, 0, normalizedFitmentRows.Count);

            return new SupplierItemSearchPage(
                cleanQuery,
                cleanSupplierCode,
                cleanVehicleType,
                selectedYear,
                cleanMake,
                cleanModel,
                cleanCategory,
                cleanBrand,
                cleanTireBrand,
                cleanTireModelLine,
                request.TireWidth,
                request.TireAspectRatio,
                request.TireRimDiameter,
                cleanTirePosition,
                availableSuppliers,
                configuredSuppliers,
                availableCategories,
                availableBrands,
                availableVehicleTypes,
                availableYears,
                availableMakes,
                availableModels,
                offset + normalizedRows.Count,
                offset,
                cappedLimit,
                normalizedHasMore,
                normalizedResults,
                isSearchExecuted,
                true,
                totalStopwatch.ElapsedMilliseconds);
        }

        var identifierCandidateIds = Array.Empty<Guid>();
        if (isIdentifierSearch && hasTextSearch && !hasYmmSearch && !hasTireSearch && !hasCategoryFilter && !hasBrandFilter)
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

        var rows = isIdentifierSearch && hasTextSearch && !hasYmmSearch && !hasTireSearch && !hasCategoryFilter && !hasBrandFilter
            ? await TimedAsync("identifier-item-query", () => (
                from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
                join globalProduct in dbContext.GlobalProducts.AsNoTracking()
                    on supplierProduct.GlobalProductId equals globalProduct.Id
                join supplier in dbContext.Suppliers.AsNoTracking()
                    on supplierProduct.SupplierId equals supplier.Id
                let baseTitle =
                    supplier.Code == "TURN14" && globalProduct.LongDescription != null
                        ? globalProduct.LongDescription
                        : supplierProduct.SupplierDescription ?? globalProduct.Description
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
                    globalProduct.LongDescription,
                    globalProduct.SpecificationsJson,
                    Status = supplierProduct.SupplierStatus,
                    supplierProduct.CaseQuantity,
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
                let baseTitle =
                    supplier.Code == "TURN14" && globalProduct.LongDescription != null
                        ? globalProduct.LongDescription
                        : supplierProduct.SupplierDescription ?? globalProduct.Description
                let maker = globalProduct.Manufacturer ?? globalProduct.Brand
                let displayPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber
                let formattedTitle =
                    supplier.Code == "TURN14" && globalProduct.LongDescription != null
                        ? baseTitle + " - " + (displayPartNumber ?? string.Empty)
                        : maker + " " + baseTitle + " - " + (displayPartNumber ?? string.Empty)
                let searchableText = (
                    supplierProduct.SupplierSku + " " +
                    (supplierProduct.SupplierPartNumber ?? string.Empty) + " " +
                    (supplierProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                    (supplierProduct.SupplierDescription ?? string.Empty) + " " +
                    baseTitle + " " +
                    formattedTitle + " " +
                    (globalProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                    (globalProduct.Manufacturer ?? string.Empty) + " " +
                    globalProduct.Brand + " " +
                    globalProduct.Description + " " +
                    (globalProduct.Category ?? string.Empty) + " " +
                    (globalProduct.LongDescription ?? string.Empty)).ToLower()
                where
                    ymmCandidateIds.Contains(supplierProduct.Id) &&
                    (cleanCategory == null || globalProduct.Category == cleanCategory) &&
                    (cleanBrand == null || globalProduct.Brand == cleanBrand) &&
                    (!hasTireSearch ||
                        ((cleanTireBrand == null || (usePostgresLike ? EF.Functions.ILike(globalProduct.Brand, cleanTireBrand) : globalProduct.Brand.ToLower() == cleanTireBrand.ToLower())) &&
                        (cleanTireModelLine == null || globalProduct.TireModelLine == cleanTireModelLine) &&
                        (request.TireWidth == null || globalProduct.TireWidth == request.TireWidth) &&
                        (request.TireAspectRatio == null || globalProduct.TireAspectRatio == request.TireAspectRatio) &&
                        (request.TireRimDiameter == null || globalProduct.TireRimDiameter == request.TireRimDiameter) &&
                        (cleanTirePosition == null || globalProduct.TirePosition == cleanTirePosition))) &&
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
                            ((usePostgresLike &&
                                (textSearchTsQuery == null ||
                                    (EF.Functions.ToTsVector(
                                    "simple",
                                    supplierProduct.SupplierSku + " " +
                                    (supplierProduct.SupplierPartNumber ?? string.Empty) + " " +
                                    (supplierProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                                    (supplierProduct.SupplierDescription ?? string.Empty) + " " +
                                    baseTitle + " " +
                                    formattedTitle)
                                    .Matches(EF.Functions.ToTsQuery("simple", textSearchTsQuery!)) ||
                                    EF.Functions.ToTsVector(
                                    "simple",
                                    (globalProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                                    (globalProduct.Manufacturer ?? string.Empty) + " " +
                                    globalProduct.Brand + " " +
                                    globalProduct.Description + " " +
                                    baseTitle + " " +
                                    formattedTitle + " " +
                                    (globalProduct.Category ?? string.Empty) + " " +
                                    (globalProduct.LongDescription ?? string.Empty))
                                        .Matches(EF.Functions.ToTsQuery("simple", textSearchTsQuery!)))) &&
                                (numericLike0 == null || EF.Functions.ILike(searchableText, numericLike0)) &&
                                (numericLike1 == null || EF.Functions.ILike(searchableText, numericLike1)) &&
                                (numericLike2 == null || EF.Functions.ILike(searchableText, numericLike2))) ||
                            (!usePostgresLike &&
                                (searchToken0a == null || searchableText.Contains(searchToken0a) || (searchToken0b != null && searchableText.Contains(searchToken0b))) &&
                                (searchToken1a == null || searchableText.Contains(searchToken1a) || (searchToken1b != null && searchableText.Contains(searchToken1b))) &&
                                (searchToken2a == null || searchableText.Contains(searchToken2a) || (searchToken2b != null && searchableText.Contains(searchToken2b))) &&
                                (searchToken3a == null || searchableText.Contains(searchToken3a) || (searchToken3b != null && searchableText.Contains(searchToken3b))) &&
                                (searchToken4a == null || searchableText.Contains(searchToken4a) || (searchToken4b != null && searchableText.Contains(searchToken4b)))) ||
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
                    globalProduct.LongDescription,
                    globalProduct.SpecificationsJson,
                    Status = supplierProduct.SupplierStatus,
                    supplierProduct.CaseQuantity,
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
                let baseTitle =
                    supplier.Code == "TURN14" && globalProduct.LongDescription != null
                        ? globalProduct.LongDescription
                        : supplierProduct.SupplierDescription ?? globalProduct.Description
                let maker = globalProduct.Manufacturer ?? globalProduct.Brand
                let displayPartNumber = supplierProduct.ManufacturerPartNumber ?? globalProduct.ManufacturerPartNumber
                let formattedTitle =
                    supplier.Code == "TURN14" && globalProduct.LongDescription != null
                        ? baseTitle + " - " + (displayPartNumber ?? string.Empty)
                        : maker + " " + baseTitle + " - " + (displayPartNumber ?? string.Empty)
                let searchableText = (
                    supplierProduct.SupplierSku + " " +
                    (supplierProduct.SupplierPartNumber ?? string.Empty) + " " +
                    (supplierProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                    (supplierProduct.SupplierDescription ?? string.Empty) + " " +
                    baseTitle + " " +
                    formattedTitle + " " +
                    (globalProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                    (globalProduct.Manufacturer ?? string.Empty) + " " +
                    globalProduct.Brand + " " +
                    globalProduct.Description + " " +
                    (globalProduct.Category ?? string.Empty) + " " +
                    (globalProduct.LongDescription ?? string.Empty)).ToLower()
                where
                    (organizationId == null || enabledCompanySupplierCodeList.Contains(supplier.Code)) &&
                    (!hasSupplierFilter || supplier.Code == cleanSupplierCode) &&
                    (cleanCategory == null || globalProduct.Category == cleanCategory) &&
                    (cleanBrand == null || globalProduct.Brand == cleanBrand) &&
                    (!hasTireSearch ||
                        ((cleanTireBrand == null || (usePostgresLike ? EF.Functions.ILike(globalProduct.Brand, cleanTireBrand) : globalProduct.Brand.ToLower() == cleanTireBrand.ToLower())) &&
                        (cleanTireModelLine == null || globalProduct.TireModelLine == cleanTireModelLine) &&
                        (request.TireWidth == null || globalProduct.TireWidth == request.TireWidth) &&
                        (request.TireAspectRatio == null || globalProduct.TireAspectRatio == request.TireAspectRatio) &&
                        (request.TireRimDiameter == null || globalProduct.TireRimDiameter == request.TireRimDiameter) &&
                        (cleanTirePosition == null || globalProduct.TirePosition == cleanTirePosition))) &&
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
                            ((usePostgresLike &&
                                (textSearchTsQuery == null ||
                                    (EF.Functions.ToTsVector(
                                    "simple",
                                    supplierProduct.SupplierSku + " " +
                                    (supplierProduct.SupplierPartNumber ?? string.Empty) + " " +
                                    (supplierProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                                    (supplierProduct.SupplierDescription ?? string.Empty) + " " +
                                    baseTitle + " " +
                                    formattedTitle)
                                    .Matches(EF.Functions.ToTsQuery("simple", textSearchTsQuery!)) ||
                                    EF.Functions.ToTsVector(
                                    "simple",
                                    (globalProduct.ManufacturerPartNumber ?? string.Empty) + " " +
                                    (globalProduct.Manufacturer ?? string.Empty) + " " +
                                    globalProduct.Brand + " " +
                                    globalProduct.Description + " " +
                                    baseTitle + " " +
                                    formattedTitle + " " +
                                    (globalProduct.Category ?? string.Empty) + " " +
                                    (globalProduct.LongDescription ?? string.Empty))
                                        .Matches(EF.Functions.ToTsQuery("simple", textSearchTsQuery!)))) &&
                                (numericLike0 == null || EF.Functions.ILike(searchableText, numericLike0)) &&
                                (numericLike1 == null || EF.Functions.ILike(searchableText, numericLike1)) &&
                                (numericLike2 == null || EF.Functions.ILike(searchableText, numericLike2))) ||
                            (!usePostgresLike &&
                                (searchToken0a == null || searchableText.Contains(searchToken0a) || (searchToken0b != null && searchableText.Contains(searchToken0b))) &&
                                (searchToken1a == null || searchableText.Contains(searchToken1a) || (searchToken1b != null && searchableText.Contains(searchToken1b))) &&
                                (searchToken2a == null || searchableText.Contains(searchToken2a) || (searchToken2b != null && searchableText.Contains(searchToken2b))) &&
                                (searchToken3a == null || searchableText.Contains(searchToken3a) || (searchToken3b != null && searchableText.Contains(searchToken3b))) &&
                                (searchToken4a == null || searchableText.Contains(searchToken4a) || (searchToken4b != null && searchableText.Contains(searchToken4b)))) ||
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
                    globalProduct.LongDescription,
                    globalProduct.SpecificationsJson,
                    Status = supplierProduct.SupplierStatus,
                    supplierProduct.CaseQuantity,
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
                        globalProduct.LongDescription,
                        globalProduct.SpecificationsJson,
                        supplierProduct.CaseQuantity,
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
                    GroupKey(row.Brand, row.Manufacturer, row.NormalizedManufacturerPartNumber, row.ManufacturerPartNumber, row.GlobalProductId, row.SupplierProductId),
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
                            row.SupplierCode,
                            row.Manufacturer,
                            row.Brand,
                            row.SupplierDescription,
                            row.GlobalDescription,
                            row.LongDescription,
                            row.ManufacturerPartNumber),
                        row.Category,
                        row.LongDescription,
                        ProductFeatures(row.SpecificationsJson),
                        row.Status,
                        fitmentRows
                            .Where(x => x.SupplierProductId == row.SupplierProductId || (x.SupplierId == row.SupplierId && x.SupplierSku == row.SupplierSku))
                            .Select(x => x.Id)
                            .Distinct()
                            .Count(),
                        price?.Msrp,
                        price?.DealerCost,
                        companyPrices.ContainsKey(row.SupplierProductId) ? actualCost : null,
                        row.CaseQuantity,
                        FirstImageUrl(row.ImageJson),
                        HasCachedInventory(row.WarehouseAvailability),
                        CachedInventoryTotal(row.WarehouseAvailability),
                        supplierPreferences.IsPreferredSupplier(row.SupplierCode),
                        supplierPreferences.PreferredWarehouseCode(row.SupplierCode),
                        supplierPreferences.PreferredWarehouseName(row.SupplierCode),
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
                    GroupKey(row.Brand, row.Manufacturer, row.NormalizedManufacturerPartNumber, row.ManufacturerPartNumber, row.GlobalProductId, row.SupplierProductId),
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
                            row.SupplierCode,
                            row.Manufacturer,
                            row.Brand,
                            row.SupplierDescription,
                            row.GlobalDescription,
                            row.LongDescription,
                            row.ManufacturerPartNumber),
                        row.Category,
                        row.LongDescription,
                        ProductFeatures(row.SpecificationsJson),
                        row.Status,
                        crossReferenceFitmentRows
                            .Where(x => x.SupplierProductId == row.SupplierProductId || (x.SupplierId == row.SupplierId && x.SupplierSku == row.SupplierSku))
                            .Select(x => x.Id)
                            .Distinct()
                            .Count(),
                        price?.Msrp,
                        price?.DealerCost,
                        crossReferenceCompanyPrices.ContainsKey(row.SupplierProductId) ? actualCost : null,
                        row.CaseQuantity,
                        FirstImageUrl(row.ImageJson),
                        HasCachedInventory(row.WarehouseAvailability),
                        CachedInventoryTotal(row.WarehouseAvailability),
                        supplierPreferences.IsPreferredSupplier(row.SupplierCode),
                        supplierPreferences.PreferredWarehouseCode(row.SupplierCode),
                        supplierPreferences.PreferredWarehouseName(row.SupplierCode),
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
                    .OrderBy(x => ServerPreferenceRank(x.Offer))
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
                    selected.LongDescription,
                    selected.ProductFeatures,
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
            cleanCategory,
            cleanBrand,
            cleanTireBrand,
            cleanTireModelLine,
            request.TireWidth,
            request.TireAspectRatio,
            request.TireRimDiameter,
            cleanTirePosition,
            availableSuppliers,
            configuredSuppliers,
            availableCategories,
            availableBrands,
            availableVehicleTypes,
            availableYears,
            availableMakes,
            availableModels,
            offset + rows.Count,
            offset,
            cappedLimit,
            hasMore,
            results,
            isSearchExecuted,
            request.UseNormalizedCatalog,
            totalStopwatch.ElapsedMilliseconds);

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

    private async Task<SupplierPurchasePreferences> GetSupplierPurchasePreferencesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var json = await dbContext.BusinessConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.SupplierPreferencesJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return SupplierPurchasePreferences.Empty;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSupplierPurchasePreferences>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return SupplierPurchasePreferences.FromStored(stored);
        }
        catch (JsonException)
        {
            return SupplierPurchasePreferences.Empty;
        }
    }

    private static int ServerPreferenceRank(SupplierItemOfferResult offer)
    {
        if (offer.IsPreferredSupplier && offer.HasCachedInventory)
        {
            return 0;
        }

        if (offer.HasCachedInventory)
        {
            return 1;
        }

        return offer.IsPreferredSupplier ? 2 : 3;
    }

    private static int ServerPreferenceRank(string supplierCode, string? warehouseAvailability, SupplierPurchasePreferences supplierPreferences)
    {
        var isPreferred = supplierPreferences.IsPreferredSupplier(supplierCode);
        var hasInventory = HasCachedInventory(warehouseAvailability);
        if (isPreferred && hasInventory)
        {
            return 0;
        }

        if (hasInventory)
        {
            return 1;
        }

        return isPreferred ? 2 : 3;
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

    private static string GroupKey(string? brand, string? manufacturer, string? normalizedManufacturerPartNumber, string? manufacturerPartNumber, Guid globalProductId, Guid supplierProductId)
    {
        var normalized = Clean(normalizedManufacturerPartNumber) ?? ProductMatchingService.NormalizeManufacturerPartNumber(manufacturerPartNumber);
        var maker = NormalizeGroupValue(Clean(manufacturer) ?? Clean(brand));
        if (normalized is not null)
        {
            return maker is null
                ? $"mfg:{normalized}"
                : $"mfg:{maker}:{normalized}";
        }

        return globalProductId == Guid.Empty
            ? $"supplier:{supplierProductId:N}"
            : $"global:{globalProductId:N}";
    }

    private static string? NormalizeGroupValue(string? value)
    {
        if (Clean(value) is not { } clean)
        {
            return null;
        }

        var normalized = new string(clean.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return MakerAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
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

    private static bool IsUsableCatalogCategory(string? value)
    {
        return value is not null && !(value.Length == 1 && char.IsLetter(value[0]));
    }

    private static string? ProductFeatures(string? specificationsJson)
    {
        if (string.IsNullOrWhiteSpace(specificationsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(specificationsJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("productFeatures", out var features) &&
                features.ValueKind == JsonValueKind.String
                    ? Clean(features.GetString())
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string[] SearchTokens(string? query)
    {
        if (Clean(query) is not { } clean)
        {
            return [];
        }

        var tokens = new List<string>();
        var current = new List<char>();
        foreach (var character in clean)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Add(char.ToLowerInvariant(character));
                continue;
            }

            AddCurrentToken();
        }

        AddCurrentToken();

        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        void AddCurrentToken()
        {
            if (current.Count == 0)
            {
                return;
            }

            var token = new string(current.ToArray());
            current.Clear();
            if (token.Length >= 2 || token.Any(char.IsDigit))
            {
                tokens.Add(token);
            }
        }
    }

    private static string? BuildPrefixTsQuery(IReadOnlyCollection<IReadOnlyCollection<string>> tokenGroups)
    {
        return tokenGroups.Count == 0
            ? null
            : string.Join(" & ", tokenGroups.Select(group =>
                group.Count == 1
                    ? $"{group.First()}:*"
                    : $"({string.Join(" | ", group.Select(token => $"{token}:*"))})"));
    }

    private static string[] SingularPluralVariants(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsDigit))
        {
            return [token];
        }

        var variants = new List<string> { token };
        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
        {
            variants.Add($"{token[..^3]}y");
        }
        else if (token.EndsWith('y') && token.Length > 1 && !IsVowel(token[^2]))
        {
            variants.Add($"{token[..^1]}ies");
        }
        else if (token.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("zes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("ses", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add(token[..^2]);
        }
        else if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && token.Length > 2)
        {
            variants.Add(token[..^1]);
        }
        else if (token.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith('x') ||
            token.EndsWith('z') ||
            token.EndsWith('s'))
        {
            variants.Add($"{token}es");
        }
        else
        {
            variants.Add($"{token}s");
        }

        return variants
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
    }

    private static string? TokenVariantAt(IReadOnlyList<string[]> tokenGroups, int groupIndex, int variantIndex)
    {
        return groupIndex < tokenGroups.Count && variantIndex < tokenGroups[groupIndex].Length
            ? tokenGroups[groupIndex][variantIndex]
            : null;
    }

    private static bool IsVowel(char value)
    {
        return value is 'a' or 'e' or 'i' or 'o' or 'u';
    }

    private static bool IsNumericSearchToken(string token)
    {
        return token.Length > 0 && token.All(char.IsDigit);
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
        string? LongDescription,
        string? SpecificationsJson,
        int? CaseQuantity,
        string? WarehouseAvailability,
        string? ImageJson,
        string Status);

    private sealed record NormalizedCanonicalRow(
        Guid CanonicalItemId,
        string? Brand,
        string? Manufacturer,
        string? ManufacturerPartNumber,
        string? NormalizedManufacturerPartNumber,
        string? PrimaryUpc,
        string Title,
        string? Category,
        string? PrimaryImageUrl,
        string? SearchText,
        string Status);

    private sealed record NormalizedOfferRow(
        Guid CanonicalItemId,
        Guid SupplierProductId,
        Guid GlobalProductId,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        string SupplierSku,
        string? ManufacturerPartNumber,
        string? NormalizedManufacturerPartNumber,
        string? Upc,
        string? SupplierTitle,
        string? LongDescription,
        string? SpecificationsJson,
        string Status,
        decimal? Msrp,
        decimal? DealerCost,
        int? CaseQuantity,
        string? WarehouseAvailability,
        string? ImageUrl,
        string? SupplierImageJson,
        string? GlobalImageJson);

    private sealed record NormalizedFitmentRow(
        Guid CanonicalItemId,
        int Year,
        string Make,
        string Model,
        string? Submodel,
        string? Engine,
        string? Notes,
        string? VehicleType);

    private sealed record StoredSupplierPurchasePreferences(
        string? PreferredSupplierCode,
        IReadOnlyDictionary<string, string>? PreferredWarehouseCodes);

    private sealed record SupplierPurchasePreferences(
        string? PreferredSupplierCode,
        IReadOnlyDictionary<string, string> PreferredWarehouseCodes)
    {
        public static SupplierPurchasePreferences Empty { get; } = new(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public static SupplierPurchasePreferences FromStored(StoredSupplierPurchasePreferences? stored)
        {
            var preferredSupplierCode = NormalizeSupplierCode(stored?.PreferredSupplierCode);
            var warehouseCodes = stored?.PreferredWarehouseCodes is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : stored.PreferredWarehouseCodes
                    .Where(x => NormalizeSupplierCode(x.Key) is not null && Clean(x.Value) is not null)
                    .ToDictionary(x => NormalizeSupplierCode(x.Key)!, x => Clean(x.Value)!, StringComparer.OrdinalIgnoreCase);
            return new SupplierPurchasePreferences(preferredSupplierCode, warehouseCodes);
        }

        public bool IsPreferredSupplier(string supplierCode)
        {
            return PreferredSupplierCode is not null &&
                string.Equals(PreferredSupplierCode, supplierCode, StringComparison.OrdinalIgnoreCase);
        }

        public string? PreferredWarehouseCode(string supplierCode)
        {
            return PreferredWarehouseCodes.TryGetValue(supplierCode, out var warehouseCode) ? warehouseCode : null;
        }

        public string? PreferredWarehouseName(string supplierCode)
        {
            var warehouseCode = PreferredWarehouseCode(supplierCode);
            if (warehouseCode is null)
            {
                return null;
            }

            return SupplierWarehouseNames.TryGetValue(supplierCode, out var names) && names.TryGetValue(warehouseCode, out var name)
                ? name
                : warehouseCode;
        }

        private static string? NormalizeSupplierCode(string? value)
        {
            var clean = Clean(value)?.ToUpperInvariant();
            return clean is "WPS" or "TURN14" or "PU" ? clean : null;
        }
    }

    private static string BuildDisplayTitle(
        string supplierCode,
        string? manufacturer,
        string brand,
        string? supplierDescription,
        string globalDescription,
        string? longDescription,
        string? manufacturerPartNumber)
    {
        var turn14LongDescription = string.Equals(supplierCode, "TURN14", StringComparison.OrdinalIgnoreCase)
            ? Clean(longDescription)
            : null;
        var title = turn14LongDescription is not null
            ? turn14LongDescription
            : Clean(supplierDescription) ?? Clean(globalDescription) ?? Clean(manufacturerPartNumber) ?? "Supplier item";
        var maker = Clean(manufacturer) ?? Clean(brand);
        if (turn14LongDescription is null &&
            !string.IsNullOrWhiteSpace(maker) &&
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
