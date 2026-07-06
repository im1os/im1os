using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class CatalogNormalizationService(IApplicationDbContext dbContext) : ICatalogNormalizationService
{
    private const string SupplierProductsSourceTable = "supplier_products";

    public async Task<CatalogNormalizationResult> NormalizeAsync(CatalogNormalizationRequest request, CancellationToken cancellationToken)
    {
        var onlyUnlinked = request.MaxItems is > 0;
        var sourceRowsQuery =
            from supplierProduct in dbContext.SupplierProducts.AsNoTracking()
            join globalProduct in dbContext.GlobalProducts.AsNoTracking() on supplierProduct.GlobalProductId equals globalProduct.Id
            join supplier in dbContext.Suppliers.AsNoTracking() on supplierProduct.SupplierId equals supplier.Id
            where !onlyUnlinked || supplierProduct.CanonicalItemId == null
            orderby supplierProduct.UpdatedAtUtc ?? supplierProduct.CreatedAtUtc, supplierProduct.Id
            select new NormalizationSourceRow(
                supplierProduct.Id,
                supplierProduct.SupplierId,
                supplier.Code,
                supplier.Name,
                supplierProduct.GlobalProductId,
                supplierProduct.CanonicalItemId,
                supplierProduct.SupplierSku,
                supplierProduct.SupplierPartNumber,
                supplierProduct.ManufacturerPartNumber,
                supplierProduct.NormalizedManufacturerPartNumber ?? globalProduct.NormalizedManufacturerPartNumber,
                supplierProduct.SupplierDescription,
                supplierProduct.SupplierStatus,
                supplierProduct.WarehouseAvailability,
                supplierProduct.SupplierImagesJson,
                globalProduct.Brand,
                globalProduct.Manufacturer,
                globalProduct.ManufacturerPartNumber,
                globalProduct.NormalizedManufacturerPartNumber,
                globalProduct.Description,
                globalProduct.LongDescription,
                globalProduct.Category,
                globalProduct.Upc,
                globalProduct.ImagesJson,
                globalProduct.Status,
                globalProduct.IsActive);

        if (request.MaxItems is > 0)
        {
            sourceRowsQuery = sourceRowsQuery.Take(request.MaxItems.Value);
        }

        var sourceRows = await sourceRowsQuery.ToListAsync(cancellationToken);
        if (sourceRows.Count == 0)
        {
            return new CatalogNormalizationResult(request.ImportRunId, 0, 0, 0, 0, 0, 0, 0, 0);
        }
        await UpdateRunProgressAsync(
            request.ImportRunId,
            0,
            sourceRows.Count,
            $"Catalog normalization loaded {sourceRows.Count:N0} supplier products. Resolving canonical items.",
            cancellationToken);

        var brandAliasesByKey = await LoadBrandAliasesAsync(sourceRows, cancellationToken);
        var sourceKeys = sourceRows.Select(x => x.SupplierProductId.ToString("N")).ToList();
        var existingSources = await dbContext.CanonicalItemSources
            .Where(x => x.SourceTable == SupplierProductsSourceTable && sourceKeys.Contains(x.SourceKey))
            .ToListAsync(cancellationToken);
        var sourceMap = existingSources.ToDictionary(x => x.SourceKey, StringComparer.OrdinalIgnoreCase);

        var normalizedPartNumbers = sourceRows
            .Select(x => Clean(x.NormalizedManufacturerPartNumber) ?? ProductMatchingService.NormalizeManufacturerPartNumber(x.ManufacturerPartNumber) ?? ProductMatchingService.NormalizeManufacturerPartNumber(x.GlobalManufacturerPartNumber))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var brands = sourceRows
            .Select(x => CanonicalBrand(x, brandAliasesByKey))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var upcs = sourceRows
            .Select(x => CleanUpc(x.Upc))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingCanonicalItems = new List<CanonicalItem>();
        if (normalizedPartNumbers.Count > 0 && brands.Count > 0)
        {
            existingCanonicalItems.AddRange(await dbContext.CanonicalItems
                .Where(x => x.NormalizedManufacturerPartNumber != null &&
                    normalizedPartNumbers.Contains(x.NormalizedManufacturerPartNumber) &&
                    x.Brand != null &&
                    brands.Contains(x.Brand))
                .ToListAsync(cancellationToken));
        }

        if (upcs.Count > 0)
        {
            var existingIds = existingCanonicalItems.Select(x => x.Id).ToHashSet();
            var upcCanonicalItems = await dbContext.CanonicalItems
                .Where(x => x.PrimaryUpc != null && upcs.Contains(x.PrimaryUpc))
                .ToListAsync(cancellationToken);
            foreach (var item in upcCanonicalItems)
            {
                if (existingIds.Add(item.Id))
                {
                    existingCanonicalItems.Add(item);
                }
            }
        }

        var canonicalByPartAndBrand = existingCanonicalItems
            .GroupBy(x => CanonicalKey(x.Brand, x.NormalizedManufacturerPartNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var canonicalByUpc = existingCanonicalItems
            .Where(x => CleanUpc(x.PrimaryUpc) is not null)
            .GroupBy(x => CleanUpc(x.PrimaryUpc)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var canonicalById = existingCanonicalItems.ToDictionary(x => x.Id);
        if (existingSources.Count > 0)
        {
            var sourceCanonicalIds = existingSources.Select(x => x.CanonicalItemId).Distinct().Except(canonicalById.Keys).ToList();
            if (sourceCanonicalIds.Count > 0)
            {
                var sourceCanonicalItems = await dbContext.CanonicalItems
                    .Where(x => sourceCanonicalIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);
                foreach (var item in sourceCanonicalItems)
                {
                    canonicalById.TryAdd(item.Id, item);
                }
            }
        }

        var supplierProductIds = sourceRows.Select(x => x.SupplierProductId).ToList();
        var offers = await dbContext.CanonicalItemSupplierOffers
            .Where(x => supplierProductIds.Contains(x.SupplierProductId))
            .ToListAsync(cancellationToken);
        var offerBySupplierProductId = offers.ToDictionary(x => x.SupplierProductId);

        var candidateCanonicalIds = canonicalById.Keys.ToList();
        var existingCandidateOffers = candidateCanonicalIds.Count == 0
            ? []
            : await dbContext.CanonicalItemSupplierOffers
                .AsNoTracking()
                .Where(x => candidateCanonicalIds.Contains(x.CanonicalItemId))
                .Select(x => new { x.CanonicalItemId, x.SupplierCode })
                .ToListAsync(cancellationToken);
        var supplierCodesByCanonicalId = existingCandidateOffers
            .GroupBy(x => x.CanonicalItemId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(o => o.SupplierCode).Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var latestPrices = (await dbContext.SupplierPrices.AsNoTracking()
                .Where(x => supplierProductIds.Contains(x.SupplierProductId))
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.LastUpdated)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.SupplierProductId)
            .ToDictionary(x => x.Key, x => x.First());

        var createdCanonicalItems = 0;
        var updatedCanonicalItems = 0;
        var upsertedOffers = 0;
        var addedSources = 0;
        var canonicalLinksBySupplierProductId = new Dictionary<Guid, Guid>();
        var processed = 0;
        foreach (var row in sourceRows)
        {
            processed++;
            var sourceKey = row.SupplierProductId.ToString("N");
            var canonicalBrand = CanonicalBrand(row, brandAliasesByKey);
            var normalizedPartNumber = Clean(row.NormalizedManufacturerPartNumber) ??
                ProductMatchingService.NormalizeManufacturerPartNumber(row.ManufacturerPartNumber) ??
                ProductMatchingService.NormalizeManufacturerPartNumber(row.GlobalManufacturerPartNumber);
            var canonicalKey = CanonicalKey(canonicalBrand, normalizedPartNumber);
            var matchMethod = normalizedPartNumber is null ? "supplier_sku_fallback" : "new_canonical";

            CanonicalItem? canonicalItem = null;
            if (sourceMap.TryGetValue(sourceKey, out var existingSource))
            {
                canonicalById.TryGetValue(existingSource.CanonicalItemId, out canonicalItem);
                matchMethod = "existing_source_link";
            }

            if (canonicalItem is null)
            {
                canonicalItem = FindCanonicalMatch(row, normalizedPartNumber, canonicalKey, canonicalByUpc, canonicalByPartAndBrand, supplierCodesByCanonicalId, out matchMethod);
            }

            if (canonicalItem is null)
            {
                canonicalItem = new CanonicalItem
                {
                    Brand = Limit(canonicalBrand, 120),
                    Manufacturer = Limit(row.Manufacturer, 160),
                    ManufacturerPartNumber = Limit(Clean(row.GlobalManufacturerPartNumber) ?? Clean(row.ManufacturerPartNumber), 120),
                    NormalizedManufacturerPartNumber = Limit(normalizedPartNumber, 120),
                    Title = Limit(BuildTitle(row, canonicalBrand), 300) ?? row.SupplierSku,
                    Category = Limit(row.Category, 160),
                    PrimaryUpc = Limit(CleanUpc(row.Upc), 80),
                    PrimaryImageUrl = Limit(FirstImage(row.GlobalImagesJson) ?? FirstImage(row.SupplierImagesJson), 1000),
                    SearchText = BuildSearchText(row, canonicalBrand),
                    Status = Limit(Clean(row.GlobalStatus) ?? Clean(row.SupplierStatus) ?? "Active", 80) ?? "Active",
                    IsActive = row.IsActive
                };
                dbContext.CanonicalItems.Add(canonicalItem);
                canonicalById[canonicalItem.Id] = canonicalItem;
                if (normalizedPartNumber is not null)
                {
                    if (!canonicalByPartAndBrand.TryGetValue(canonicalKey, out var canonicalItemsForKey))
                    {
                        canonicalItemsForKey = [];
                        canonicalByPartAndBrand[canonicalKey] = canonicalItemsForKey;
                    }

                    canonicalItemsForKey.Add(canonicalItem);
                }

                var upc = CleanUpc(row.Upc);
                if (upc is not null)
                {
                    if (!canonicalByUpc.TryGetValue(upc, out var canonicalItemsForUpc))
                    {
                        canonicalItemsForUpc = [];
                        canonicalByUpc[upc] = canonicalItemsForUpc;
                    }

                    canonicalItemsForUpc.Add(canonicalItem);
                }

                supplierCodesByCanonicalId[canonicalItem.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                createdCanonicalItems++;
            }
            else
            {
                UpdateCanonicalItem(canonicalItem, row, canonicalBrand, normalizedPartNumber);
                updatedCanonicalItems++;
            }

            if (!sourceMap.ContainsKey(sourceKey))
            {
                var source = new CanonicalItemSource
                {
                    CanonicalItemId = canonicalItem.Id,
                    GlobalProductId = row.GlobalProductId,
                    SupplierId = row.SupplierId,
                    SupplierProductId = row.SupplierProductId,
                    SupplierCode = Limit(row.SupplierCode, 80),
                    SourceTable = SupplierProductsSourceTable,
                    SourceKey = Limit(sourceKey, 200) ?? sourceKey,
                    MatchMethod = Limit(matchMethod, 120) ?? matchMethod,
                    MatchConfidence = MatchConfidence(matchMethod)
                };
                dbContext.CanonicalItemSources.Add(source);
                sourceMap[sourceKey] = source;
                addedSources++;
            }

            latestPrices.TryGetValue(row.SupplierProductId, out var price);
            if (!offerBySupplierProductId.TryGetValue(row.SupplierProductId, out var offer))
            {
                offer = new CanonicalItemSupplierOffer
                {
                    CanonicalItemId = canonicalItem.Id,
                    SupplierId = row.SupplierId,
                    SupplierProductId = row.SupplierProductId,
                    SupplierCode = Limit(row.SupplierCode, 80) ?? row.SupplierCode,
                    SupplierSku = Limit(row.SupplierSku, 120) ?? row.SupplierSku,
                    Status = Limit(Clean(row.SupplierStatus) ?? "Active", 80) ?? "Active"
                };
                dbContext.CanonicalItemSupplierOffers.Add(offer);
                offerBySupplierProductId[row.SupplierProductId] = offer;
            }

            offer.CanonicalItemId = canonicalItem.Id;
            offer.SupplierCode = Limit(row.SupplierCode, 80) ?? row.SupplierCode;
            offer.SupplierSku = Limit(row.SupplierSku, 120) ?? row.SupplierSku;
            offer.SupplierPartNumber = Limit(row.SupplierPartNumber, 120);
            offer.SupplierTitle = Limit(Clean(row.SupplierDescription) ?? Clean(row.GlobalDescription), 500);
            offer.ListPrice = price?.Msrp;
            offer.DealerCost = price?.DealerCost;
            offer.WarehouseAvailability = ValidJsonOrNull(row.WarehouseAvailability);
            offer.ImageUrl = Limit(FirstImage(row.SupplierImagesJson) ?? FirstImage(row.GlobalImagesJson), 1000);
            offer.Status = Limit(Clean(row.SupplierStatus) ?? "Active", 80) ?? "Active";
            upsertedOffers++;
            canonicalLinksBySupplierProductId[row.SupplierProductId] = canonicalItem.Id;
            if (!supplierCodesByCanonicalId.TryGetValue(canonicalItem.Id, out var supplierCodes))
            {
                supplierCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                supplierCodesByCanonicalId[canonicalItem.Id] = supplierCodes;
            }

            supplierCodes.Add(row.SupplierCode);

            if (processed % 1000 == 0 || processed == sourceRows.Count)
            {
                await UpdateRunProgressAsync(
                    request.ImportRunId,
                    processed,
                    sourceRows.Count,
                    $"Catalog normalization resolving canonical items. Processed {processed:N0} / {sourceRows.Count:N0}; created {createdCanonicalItems:N0}, updated {updatedCanonicalItems:N0}, offers {upsertedOffers:N0}.",
                    cancellationToken);
            }
        }

        await UpdateRunProgressAsync(
            request.ImportRunId,
            processed,
            sourceRows.Count,
            $"Catalog normalization saving {upsertedOffers:N0} supplier offers and {addedSources:N0} source links.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await UpdateRunProgressAsync(
            request.ImportRunId,
            processed,
            sourceRows.Count,
            $"Catalog normalization linking {sourceRows.Count:N0} supplier products to canonical items.",
            cancellationToken);
        var linkedSupplierProducts = await dbContext.SupplierProducts
            .Where(x => supplierProductIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var supplierProduct in linkedSupplierProducts)
        {
            if (canonicalLinksBySupplierProductId.TryGetValue(supplierProduct.Id, out var canonicalItemId))
            {
                supplierProduct.CanonicalItemId = canonicalItemId;
            }
        }

        var canonicalItemIds = sourceRows
            .Select(x => sourceMap[x.SupplierProductId.ToString("N")].CanonicalItemId)
            .Distinct()
            .ToList();
        await UpdateRunProgressAsync(
            request.ImportRunId,
            processed,
            sourceRows.Count,
            "Catalog normalization adding item identifiers.",
            cancellationToken);
        var addedIdentifiers = await AddIdentifiersAsync(sourceRows, sourceMap, canonicalItemIds, cancellationToken);
        await UpdateRunProgressAsync(
            request.ImportRunId,
            processed,
            sourceRows.Count,
            $"Catalog normalization adding canonical fitment rows. Added {addedIdentifiers:N0} identifiers.",
            cancellationToken);
        var addedFitments = await AddFitmentsAsync(sourceRows, sourceMap, canonicalItemIds, cancellationToken);
        await UpdateRunProgressAsync(
            request.ImportRunId,
            processed,
            sourceRows.Count,
            $"Catalog normalization saving identifiers and fitment. Added {addedIdentifiers:N0} identifiers and {addedFitments:N0} fitments.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogNormalizationResult(
            request.ImportRunId,
            sourceRows.Count,
            createdCanonicalItems,
            updatedCanonicalItems,
            upsertedOffers,
            addedIdentifiers,
            addedSources,
            addedFitments,
            0);
    }

    private async Task UpdateRunProgressAsync(Guid importRunId, int processed, int total, string message, CancellationToken cancellationToken)
    {
        await dbContext.SupplierConnectorImportRuns
            .Where(x => x.Id == importRunId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProgressProcessed, processed)
                .SetProperty(x => x.ProgressTotal, total)
                .SetProperty(x => x.Message, message),
                cancellationToken);
    }

    private async Task<int> AddIdentifiersAsync(
        IReadOnlyCollection<NormalizationSourceRow> sourceRows,
        IReadOnlyDictionary<string, CanonicalItemSource> sourceMap,
        IReadOnlyCollection<Guid> canonicalItemIds,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.CanonicalItemIdentifiers
            .Where(x => canonicalItemIds.Contains(x.CanonicalItemId))
            .Select(x => new
            {
                x.CanonicalItemId,
                x.IdentifierType,
                x.NormalizedValue,
                x.SupplierProductId
            })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(x => IdentifierKey(x.CanonicalItemId, x.IdentifierType, x.NormalizedValue, x.SupplierProductId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var row in sourceRows)
        {
            var canonicalItemId = sourceMap[row.SupplierProductId.ToString("N")].CanonicalItemId;
            added += AddIdentifier(existingKeys, canonicalItemId, "supplier_sku", row.SupplierSku, row.SupplierSku, row, false);
            added += AddIdentifier(existingKeys, canonicalItemId, "supplier_part_number", row.SupplierPartNumber, ProductMatchingService.NormalizeManufacturerPartNumber(row.SupplierPartNumber), row, false);
            added += AddIdentifier(existingKeys, canonicalItemId, "manufacturer_part_number", row.ManufacturerPartNumber ?? row.GlobalManufacturerPartNumber, row.NormalizedManufacturerPartNumber, row, true);
            added += AddIdentifier(existingKeys, canonicalItemId, "upc", row.Upc, CleanUpc(row.Upc), row, true);
        }

        return added;
    }

    private static CanonicalItem? FindCanonicalMatch(
        NormalizationSourceRow row,
        string? normalizedPartNumber,
        string canonicalKey,
        IReadOnlyDictionary<string, List<CanonicalItem>> canonicalByUpc,
        IReadOnlyDictionary<string, List<CanonicalItem>> canonicalByPartAndBrand,
        IReadOnlyDictionary<Guid, HashSet<string>> supplierCodesByCanonicalId,
        out string matchMethod)
    {
        var upc = CleanUpc(row.Upc);
        if (upc is not null &&
            canonicalByUpc.TryGetValue(upc, out var upcMatches) &&
            upcMatches.Count == 1)
        {
            matchMethod = "upc";
            return upcMatches[0];
        }

        if (normalizedPartNumber is null ||
            !canonicalByPartAndBrand.TryGetValue(canonicalKey, out var partMatches) ||
            partMatches.Count == 0)
        {
            matchMethod = "new_canonical";
            return null;
        }

        var crossSupplierMatches = partMatches
            .Where(x =>
                supplierCodesByCanonicalId.TryGetValue(x.Id, out var supplierCodes) &&
                supplierCodes.Count > 0 &&
                supplierCodes.Any(code => !string.Equals(code, row.SupplierCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (crossSupplierMatches.Count == 0)
        {
            matchMethod = "same_supplier_normalized_part_not_merged";
            return null;
        }

        var rawPartNumber = Clean(row.GlobalManufacturerPartNumber) ?? Clean(row.ManufacturerPartNumber);
        var rawMatch = crossSupplierMatches.FirstOrDefault(x =>
            string.Equals(Clean(x.ManufacturerPartNumber), rawPartNumber, StringComparison.OrdinalIgnoreCase));
        if (rawMatch is not null)
        {
            matchMethod = "cross_supplier_brand_raw_mfg_part";
            return rawMatch;
        }

        matchMethod = "cross_supplier_brand_normalized_mfg_part";
        return crossSupplierMatches
            .OrderByDescending(x => supplierCodesByCanonicalId.TryGetValue(x.Id, out var supplierCodes) ? supplierCodes.Count : 0)
            .ThenBy(x => x.CreatedAtUtc)
            .First();
    }

    private static decimal MatchConfidence(string matchMethod)
    {
        return matchMethod switch
        {
            "existing_source_link" => 1.00m,
            "upc" => 0.99m,
            "cross_supplier_brand_raw_mfg_part" => 0.97m,
            "cross_supplier_brand_normalized_mfg_part" => 0.92m,
            "same_supplier_normalized_part_not_merged" => 0.70m,
            _ => 0.60m
        };
    }

    private int AddIdentifier(
        ISet<string> existingKeys,
        Guid canonicalItemId,
        string identifierType,
        string? value,
        string? normalizedValue,
        NormalizationSourceRow row,
        bool isPrimary)
    {
        var cleanValue = Clean(value);
        var cleanNormalizedValue = Clean(normalizedValue);
        if (cleanValue is null || cleanNormalizedValue is null)
        {
            return 0;
        }

        var key = IdentifierKey(canonicalItemId, identifierType, cleanNormalizedValue, row.SupplierProductId);
        if (!existingKeys.Add(key))
        {
            return 0;
        }

        dbContext.CanonicalItemIdentifiers.Add(new CanonicalItemIdentifier
        {
            CanonicalItemId = canonicalItemId,
            IdentifierType = identifierType,
            IdentifierValue = Limit(cleanValue, 200) ?? cleanValue,
            NormalizedValue = Limit(cleanNormalizedValue, 200) ?? cleanNormalizedValue,
            SupplierId = row.SupplierId,
            SupplierCode = Limit(row.SupplierCode, 80),
            SupplierProductId = row.SupplierProductId,
            Source = SupplierProductsSourceTable,
            IsPrimary = isPrimary
        });
        return 1;
    }

    private async Task<int> AddFitmentsAsync(
        IReadOnlyCollection<NormalizationSourceRow> sourceRows,
        IReadOnlyDictionary<string, CanonicalItemSource> sourceMap,
        IReadOnlyCollection<Guid> canonicalItemIds,
        CancellationToken cancellationToken)
    {
        var supplierProductIds = sourceRows.Select(x => x.SupplierProductId).ToList();
        var globalProductIds = sourceRows.Select(x => x.GlobalProductId).Distinct().ToList();
        var canonicalBySupplierProductId = sourceRows.ToDictionary(
            x => x.SupplierProductId,
            x => sourceMap[x.SupplierProductId.ToString("N")].CanonicalItemId);
        var canonicalByGlobalProductId = sourceRows
            .GroupBy(x => x.GlobalProductId)
            .ToDictionary(x => x.Key, x => sourceMap[x.First().SupplierProductId.ToString("N")].CanonicalItemId);

        var sourceFitments = await dbContext.SupplierFitmentRecords.AsNoTracking()
            .Where(x =>
                (x.SupplierProductId != null && supplierProductIds.Contains(x.SupplierProductId.Value)) ||
                (x.GlobalProductId != null && globalProductIds.Contains(x.GlobalProductId.Value)))
            .ToListAsync(cancellationToken);
        if (sourceFitments.Count == 0)
        {
            return 0;
        }

        var existing = await dbContext.CanonicalFitments
            .Where(x => canonicalItemIds.Contains(x.CanonicalItemId))
            .Select(x => new { x.CanonicalItemId, x.Year, x.MakeKey, x.ModelKey, x.SubmodelKey, x.EngineKey })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(x => FitmentKey(x.CanonicalItemId, x.Year, x.MakeKey, x.ModelKey, x.SubmodelKey, x.EngineKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var fitment in sourceFitments)
        {
            Guid? canonicalItemId = null;
            if (fitment.SupplierProductId is Guid supplierProductId &&
                canonicalBySupplierProductId.TryGetValue(supplierProductId, out var supplierCanonicalItemId))
            {
                canonicalItemId = supplierCanonicalItemId;
            }
            else if (fitment.GlobalProductId is Guid globalProductId &&
                canonicalByGlobalProductId.TryGetValue(globalProductId, out var globalCanonicalItemId))
            {
                canonicalItemId = globalCanonicalItemId;
            }

            if (canonicalItemId is null || fitment.Year <= 0 || string.IsNullOrWhiteSpace(fitment.Make) || string.IsNullOrWhiteSpace(fitment.Model))
            {
                continue;
            }

            var makeKey = Limit(NormalizeFitmentKey(fitment.Make), 120) ?? string.Empty;
            var modelKey = Limit(NormalizeFitmentKey(fitment.Model), 160) ?? string.Empty;
            var submodelKey = Limit(NormalizeFitmentKey(fitment.Submodel), 160) ?? string.Empty;
            var engineKey = Limit(NormalizeFitmentKey(fitment.Engine), 160) ?? string.Empty;
            var key = FitmentKey(canonicalItemId.Value, fitment.Year, makeKey, modelKey, submodelKey, engineKey);
            if (!existingKeys.Add(key))
            {
                continue;
            }

            dbContext.CanonicalFitments.Add(new CanonicalFitment
            {
                CanonicalItemId = canonicalItemId.Value,
                Year = fitment.Year,
                Make = Limit(fitment.Make, 120) ?? fitment.Make.Trim(),
                MakeKey = makeKey,
                Model = Limit(fitment.Model, 160) ?? fitment.Model.Trim(),
                ModelKey = modelKey,
                VehicleType = Limit(Clean(fitment.VehicleType) ?? Clean(fitment.VehicleClass), 120),
                Submodel = Limit(fitment.Submodel, 160),
                SubmodelKey = submodelKey,
                Engine = Limit(fitment.Engine, 160),
                EngineKey = engineKey,
                Notes = Limit(fitment.Notes, 1000)
            });
            added++;
        }

        return added;
    }

    private static void UpdateCanonicalItem(CanonicalItem item, NormalizationSourceRow row, string? canonicalBrand, string? normalizedPartNumber)
    {
        item.Brand ??= Limit(canonicalBrand, 120);
        item.Manufacturer ??= Limit(row.Manufacturer, 160);
        item.ManufacturerPartNumber ??= Limit(Clean(row.GlobalManufacturerPartNumber) ?? Clean(row.ManufacturerPartNumber), 120);
        item.NormalizedManufacturerPartNumber ??= Limit(normalizedPartNumber, 120);
        item.Category ??= Limit(row.Category, 160);
        item.PrimaryUpc ??= Limit(CleanUpc(row.Upc), 80);
        item.PrimaryImageUrl ??= Limit(FirstImage(row.GlobalImagesJson) ?? FirstImage(row.SupplierImagesJson), 1000);
        item.SearchText = BuildSearchText(row, canonicalBrand);
        item.Status = Limit(Clean(row.GlobalStatus) ?? Clean(row.SupplierStatus) ?? item.Status, 80) ?? item.Status;
        item.IsActive = item.IsActive || row.IsActive;
    }

    private static string BuildTitle(NormalizationSourceRow row, string? canonicalBrand)
    {
        var description = Clean(row.GlobalDescription) ?? Clean(row.SupplierDescription);
        var partNumber = Clean(row.GlobalManufacturerPartNumber) ?? Clean(row.ManufacturerPartNumber);
        return partNumber is null || description?.Contains(partNumber, StringComparison.OrdinalIgnoreCase) == true
            ? description ?? row.SupplierSku
            : $"{description ?? canonicalBrand ?? row.Brand} - {partNumber}";
    }

    private static string BuildSearchText(NormalizationSourceRow row, string? canonicalBrand)
    {
        return string.Join(" ", new[]
        {
            canonicalBrand,
            row.Brand,
            row.Manufacturer,
            row.GlobalDescription,
            row.LongDescription,
            row.SupplierDescription,
            row.SupplierSku,
            row.SupplierPartNumber,
            row.ManufacturerPartNumber,
            row.GlobalManufacturerPartNumber,
            row.Upc,
            row.Category
        }.Select(Clean).Where(x => x is not null));
    }

    private static string? FirstImage(string? json)
    {
        var clean = Clean(json);
        if (clean is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(clean);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        return Clean(item.GetString());
                    }

                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
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
            return clean.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? clean : null;
        }

        return null;
    }

    private static string? ValidJsonOrNull(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return null;
        }

        try
        {
            using var _ = JsonDocument.Parse(clean);
            return clean;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string CanonicalKey(string? brand, string? normalizedManufacturerPartNumber)
    {
        return $"{Clean(brand)?.ToUpperInvariant() ?? ""}|{Clean(normalizedManufacturerPartNumber)?.ToUpperInvariant() ?? ""}";
    }

    private static string IdentifierKey(Guid canonicalItemId, string identifierType, string normalizedValue, Guid? supplierProductId)
    {
        return $"{canonicalItemId:N}|{identifierType}|{normalizedValue.ToUpperInvariant()}|{supplierProductId:N}";
    }

    private static string FitmentKey(Guid canonicalItemId, int year, string make, string model, string? submodel, string? engine)
    {
        return $"{canonicalItemId:N}|{year}|{make}|{model}|{submodel ?? ""}|{engine ?? ""}";
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadBrandAliasesAsync(
        IReadOnlyCollection<NormalizationSourceRow> sourceRows,
        CancellationToken cancellationToken)
    {
        var brandKeys = sourceRows
            .Select(x => NormalizeBrandKey(x.Brand))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (brandKeys.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var aliases = await dbContext.CanonicalBrandAliases
            .AsNoTracking()
            .Where(x => x.IsActive && brandKeys.Contains(x.NormalizedBrand))
            .Select(x => new { x.NormalizedBrand, x.CanonicalBrand })
            .ToListAsync(cancellationToken);

        return aliases.ToDictionary(x => x.NormalizedBrand, x => x.CanonicalBrand, StringComparer.OrdinalIgnoreCase);
    }

    private static string? CanonicalBrand(NormalizationSourceRow row, IReadOnlyDictionary<string, string> brandAliasesByKey)
    {
        var brand = Clean(row.Brand);
        if (brand is null)
        {
            return null;
        }

        var brandKey = NormalizeBrandKey(brand);
        return brandKey is not null && brandAliasesByKey.TryGetValue(brandKey, out var canonicalBrand)
            ? canonicalBrand
            : brand;
    }

    private static string? NormalizeBrandKey(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return null;
        }

        var chars = clean
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();
        return chars.Length == 0 ? null : new string(chars);
    }

    private static string NormalizeFitmentKey(string? value)
    {
        var clean = Clean(value);
        return clean is null
            ? string.Empty
            : string.Join(" ", clean.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? CleanUpc(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return null;
        }

        var digits = new string(clean.Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 14 || digits.All(x => x == '0'))
        {
            return null;
        }

        return digits;
    }

    private static string? Limit(string? value, int maxLength)
    {
        var clean = Clean(value);
        if (clean is null || clean.Length <= maxLength)
        {
            return clean;
        }

        return clean[..maxLength];
    }

    private sealed record NormalizationSourceRow(
        Guid SupplierProductId,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        Guid GlobalProductId,
        Guid? CanonicalItemId,
        string SupplierSku,
        string? SupplierPartNumber,
        string? ManufacturerPartNumber,
        string? NormalizedManufacturerPartNumber,
        string? SupplierDescription,
        string SupplierStatus,
        string? WarehouseAvailability,
        string? SupplierImagesJson,
        string Brand,
        string? Manufacturer,
        string? GlobalManufacturerPartNumber,
        string? GlobalNormalizedManufacturerPartNumber,
        string GlobalDescription,
        string? LongDescription,
        string? Category,
        string? Upc,
        string? GlobalImagesJson,
        string GlobalStatus,
        bool IsActive);
}
