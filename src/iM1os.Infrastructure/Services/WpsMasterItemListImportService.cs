using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class WpsMasterItemListImportService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<WpsMasterItemListImportService> logger) : IWpsMasterItemListImportService
{
    private const int MasterFileWriteBatchSize = 1000;
    private const string WpsSupplierCode = "WPS";
    private const string WpsSupplierName = "Western Power Sports";
    private const string WpsConnectorKey = "WPS";
    private const string DefaultMasterFileUrl = "https://data-depot.s3.us-west-2.amazonaws.com/v4/downloads/master-item-list/master-item-list.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WpsMasterItemListImportResult> ImportAsync(WpsMasterItemListImportRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleAsync(x => x.Id == importRun.SupplierConnectorConfigurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);

        if (!string.Equals(supplier.Code, WpsSupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Import run {request.ImportRunId} is not a WPS import run.");
        }

        var sourceUrl = Clean(configuration.MasterFileUrl) ?? Clean(importRun.Source) ?? DefaultMasterFileUrl;
        var maxItems = request.MaxItems ?? ReadMaxItems(importRun.ParametersJson);
        var now = dateTimeProvider.UtcNow;
        var effectiveDate = DateOnly.FromDateTime(now.UtcDateTime);

        importRun.Status = "Running";
        importRun.StartedAtUtc = now;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = maxItems;
        importRun.Message = maxItems is null
            ? "WPS Master Item List import started."
            : $"WPS Master Item List import started. Processing first {maxItems.Value} items.";
        await dbContext.SaveChangesAsync(cancellationToken);

        var client = httpClientFactory.CreateClient("WpsDataDepot");
        using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var counters = await ImportItemsAsync(supplier.Id, stream, maxItems, effectiveDate, now, importRun, cancellationToken);

        TrackImportRun(importRun);
        importRun.ProgressProcessed = counters.Processed;
        await dbContext.SaveChangesAsync(cancellationToken);

        TrackImportRun(importRun);
        importRun.Status = "Completed";
        importRun.CompletedAtUtc = dateTimeProvider.UtcNow;
        importRun.ProgressProcessed = counters.Processed;
        importRun.ProgressTotal ??= counters.Processed;
        importRun.Message = $"WPS Master Item List import completed. Processed {counters.Processed} items, created {counters.CreatedGlobalProducts} global products, created {counters.CreatedSupplierProducts} supplier products.";
        await dbContext.SaveChangesAsync(cancellationToken);

        return new WpsMasterItemListImportResult(
            importRun.Id,
            counters.Processed,
            counters.CreatedGlobalProducts,
            counters.UpdatedGlobalProducts,
            counters.CreatedSupplierProducts,
            counters.UpdatedSupplierProducts,
            counters.UpsertedPrices);
    }

    public static async Task<Guid> EnsureWpsImportRunAsync(IApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider, int? maxItems, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Code == WpsSupplierCode, cancellationToken);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = WpsSupplierName,
                Code = WpsSupplierCode,
                ConnectorKey = WpsConnectorKey,
                IsActive = true
            };
            dbContext.Suppliers.Add(supplier);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == WpsConnectorKey, cancellationToken);
        if (configuration is null)
        {
            configuration = new SupplierConnectorConfiguration
            {
                SupplierId = supplier.Id,
                ConnectorKey = WpsConnectorKey,
                DisplayName = "WPS",
                BaseApiUrl = "https://api.wps-inc.com",
                MasterFileUrl = DefaultMasterFileUrl,
                AuthMode = "MasterFileUrl",
                IsEnabled = true,
                ImportMasterFileOnSchedule = false,
                MasterFileImportMode = "Manual"
            };
            dbContext.SupplierConnectorConfigurations.Add(configuration);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(configuration.MasterFileUrl))
        {
            configuration.MasterFileUrl = DefaultMasterFileUrl;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = dateTimeProvider.UtcNow;
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "WpsMasterFile",
            Status = "Queued",
            RequestedAtUtc = now,
            Source = configuration.MasterFileUrl ?? DefaultMasterFileUrl,
            ProgressProcessed = 0,
            ProgressTotal = maxItems,
            ParametersJson = JsonSerializer.Serialize(new
            {
                ImportProducts = true,
                ImportSupplierPricing = true,
                ImportFitment = false,
                UpdateExistingProducts = true,
                CreateMissingProducts = true,
                ImportMode = "LimitedTest",
                MaxItems = maxItems
            }),
            Message = maxItems is null
                ? "WPS Master Item List import queued."
                : $"WPS Master Item List import queued for first {maxItems.Value} items."
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        return importRun.Id;
    }

    private async Task<ImportCounters> ImportItemsAsync(
        Guid supplierId,
        Stream stream,
        int? maxItems,
        DateOnly effectiveDate,
        DateTimeOffset now,
        SupplierConnectorImportRun importRun,
        CancellationToken cancellationToken)
    {
        var counters = new ImportCounters();
        var batch = new List<WpsImportItem>(MasterFileWriteBatchSize);
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream, JsonOptions, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind == JsonValueKind.Object)
            {
                batch.Add(WpsImportItem.From(item));
            }

            if (batch.Count < MasterFileWriteBatchSize && (maxItems is null || counters.Processed + batch.Count < maxItems.Value))
            {
                continue;
            }

            await UpsertItemBatchAsync(supplierId, batch, effectiveDate, now, counters, cancellationToken);
            await SaveMasterBatchAsync(importRun, counters.Processed, cancellationToken);
            logger.LogInformation("Imported {Count} WPS master item rows for run {ImportRunId}.", counters.Processed, importRun.Id);
            batch.Clear();

            if (maxItems is not null && counters.Processed >= maxItems.Value)
            {
                break;
            }
        }

        if (batch.Count > 0 && (maxItems is null || counters.Processed < maxItems.Value))
        {
            await UpsertItemBatchAsync(supplierId, batch, effectiveDate, now, counters, cancellationToken);
            await SaveMasterBatchAsync(importRun, counters.Processed, cancellationToken);
            logger.LogInformation("Imported {Count} WPS master item rows for run {ImportRunId}.", counters.Processed, importRun.Id);
        }

        importRun.ProgressProcessed = counters.Processed;
        return counters;
    }

    private async Task UpsertItemBatchAsync(
        Guid supplierId,
        IReadOnlyCollection<WpsImportItem> items,
        DateOnly effectiveDate,
        DateTimeOffset now,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var skus = items
            .Select(x => x.Sku)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var supplierProducts = await dbContext.SupplierProducts
            .Where(x => x.SupplierId == supplierId && skus.Contains(x.SupplierSku))
            .ToListAsync(cancellationToken);
        var supplierProductsBySku = supplierProducts.ToDictionary(x => x.SupplierSku, StringComparer.OrdinalIgnoreCase);

        var globalProductIds = supplierProducts
            .Select(x => x.GlobalProductId)
            .Distinct()
            .ToArray();
        var globalProductsById = globalProductIds.Length == 0
            ? new Dictionary<Guid, GlobalProduct>()
            : await dbContext.GlobalProducts
                .Where(x => globalProductIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var normalizedPartNumbers = items
            .Select(x => x.NormalizedManufacturerPartNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var vendorNumbers = items
            .Select(x => x.VendorNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var globalCandidates = normalizedPartNumbers.Length == 0 && vendorNumbers.Length == 0
            ? new List<GlobalProduct>()
            : await dbContext.GlobalProducts
                .Where(x =>
                    (x.NormalizedManufacturerPartNumber != null && normalizedPartNumbers.Contains(x.NormalizedManufacturerPartNumber)) ||
                    (x.ManufacturerPartNumber != null && vendorNumbers.Contains(x.ManufacturerPartNumber)))
                .ToListAsync(cancellationToken);

        var globalProductsByNormalizedKey = new Dictionary<string, GlobalProduct>(StringComparer.OrdinalIgnoreCase);
        var globalProductsByManufacturerKey = new Dictionary<string, GlobalProduct>(StringComparer.Ordinal);
        foreach (var globalProduct in globalProductsById.Values.Concat(globalCandidates).OrderBy(x => x.Id))
        {
            AddGlobalProductLookup(globalProductsByNormalizedKey, globalProductsByManufacturerKey, globalProduct);
        }

        var existingSupplierProductIds = supplierProducts
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
        var existingPrices = existingSupplierProductIds.Length == 0
            ? new List<SupplierPrice>()
            : await dbContext.SupplierPrices
                .Where(x => existingSupplierProductIds.Contains(x.SupplierProductId) && x.EffectiveDate == effectiveDate)
                .ToListAsync(cancellationToken);
        var pricesBySupplierProductId = existingPrices
            .GroupBy(x => x.SupplierProductId)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Id).First());

        foreach (var item in items)
        {
            UpsertItem(item);
        }

        void UpsertItem(WpsImportItem item)
        {
            var supplierProductExists = supplierProductsBySku.TryGetValue(item.Sku, out var supplierProduct);
            var currentGlobalProduct = supplierProductExists
                ? globalProductsById[supplierProduct!.GlobalProductId]
                : null;
            var globalProduct = FindGlobalProductInBatch(item, globalProductsByNormalizedKey, globalProductsByManufacturerKey);
            if (globalProduct is not null && !CanUseGlobalProductForExactKey(globalProduct, item.Brand, item.VendorNumber))
            {
                globalProduct = null;
            }

            if (globalProduct is null &&
                currentGlobalProduct is not null &&
                CanUseGlobalProductForExactKey(currentGlobalProduct, item.Brand, item.VendorNumber))
            {
                globalProduct = currentGlobalProduct;
            }

            if (globalProduct is null)
            {
                globalProduct = new GlobalProduct
                {
                    Brand = item.Brand,
                    Manufacturer = item.Brand,
                    ManufacturerPartNumber = item.VendorNumber,
                    NormalizedManufacturerPartNumber = item.NormalizedManufacturerPartNumber,
                    Description = item.ProductName ?? item.Name,
                    LongDescription = item.ProductDescription ?? item.CatalogGroupDescription,
                    Category = item.ProductType,
                    Upc = item.Upc,
                    Length = item.Length,
                    Width = item.Width,
                    Height = item.Height,
                    Weight = item.Weight,
                    ImagesJson = item.ImagesJson,
                    SpecificationsJson = item.SpecificationsJson,
                    Status = item.Status == "NLA" ? "Inactive" : "Active",
                    IsActive = item.Status != "NLA"
                };
                dbContext.GlobalProducts.Add(globalProduct);
                AddGlobalProductLookup(globalProductsByNormalizedKey, globalProductsByManufacturerKey, globalProduct);
                counters.CreatedGlobalProducts++;
            }
            else
            {
                globalProduct.Brand = item.Brand;
                globalProduct.Manufacturer ??= item.Brand;
                globalProduct.ManufacturerPartNumber ??= item.VendorNumber;
                globalProduct.NormalizedManufacturerPartNumber ??= item.NormalizedManufacturerPartNumber;
                globalProduct.Description = item.ProductName ?? item.Name;
                globalProduct.LongDescription = item.ProductDescription ?? globalProduct.LongDescription ?? item.CatalogGroupDescription;
                globalProduct.Category = item.ProductType ?? globalProduct.Category;
                globalProduct.Upc ??= item.Upc;
                globalProduct.Length = item.Length ?? globalProduct.Length;
                globalProduct.Width = item.Width ?? globalProduct.Width;
                globalProduct.Height = item.Height ?? globalProduct.Height;
                globalProduct.Weight = item.Weight ?? globalProduct.Weight;
                globalProduct.ImagesJson = item.ImagesJson ?? globalProduct.ImagesJson;
                globalProduct.SpecificationsJson = item.SpecificationsJson;
                globalProduct.Status = item.Status == "NLA" ? "Inactive" : "Active";
                globalProduct.IsActive = item.Status != "NLA";
                AddGlobalProductLookup(globalProductsByNormalizedKey, globalProductsByManufacturerKey, globalProduct);
                counters.UpdatedGlobalProducts++;
            }

            CatalogTireParser.Apply(
                globalProduct,
                item.Brand,
                item.ProductType,
                item.Name,
                item.ProductName,
                item.ProductDescription,
                item.CatalogGroupDescription,
                item.SpecificationsJson);

            if (supplierProduct is null)
            {
                supplierProduct = new SupplierProduct
                {
                    SupplierId = supplierId,
                    GlobalProductId = globalProduct.Id,
                    SupplierSku = item.Sku,
                    SupplierStatus = item.Status
                };
                dbContext.SupplierProducts.Add(supplierProduct);
                supplierProductsBySku[item.Sku] = supplierProduct;
                counters.CreatedSupplierProducts++;
            }
            else
            {
                counters.UpdatedSupplierProducts++;
            }

            supplierProduct.GlobalProductId = globalProduct.Id;
            supplierProduct.SupplierDescription = item.Name;
            supplierProduct.SupplierPartNumber = item.Sku;
            supplierProduct.ManufacturerPartNumber = item.VendorNumber;
            supplierProduct.NormalizedManufacturerPartNumber = item.NormalizedManufacturerPartNumber;
            supplierProduct.SupplierStatus = item.Status;
            supplierProduct.SupplierImagesJson = item.ImagesJson;
            supplierProduct.SourceDataJson = item.SourceDataJson;
            supplierProduct.LastSyncedAtUtc = now;

            if (!pricesBySupplierProductId.TryGetValue(supplierProduct.Id, out var price))
            {
                price = new SupplierPrice
                {
                    SupplierProductId = supplierProduct.Id,
                    EffectiveDate = effectiveDate
                };
                dbContext.SupplierPrices.Add(price);
                pricesBySupplierProductId[supplierProduct.Id] = price;
            }

            price.Msrp = item.Msrp;
            price.Map = item.Map;
            price.DealerCost = 0m;
            price.LastUpdated = now;
            counters.UpsertedPrices++;
            counters.Processed++;
        }
    }

    private async Task SaveMasterBatchAsync(SupplierConnectorImportRun importRun, int processed, CancellationToken cancellationToken)
    {
        TrackImportRun(importRun);
        importRun.ProgressProcessed = processed;
        await dbContext.SaveChangesAsync(cancellationToken);
        ClearChangeTracker();
    }

    private void TrackImportRun(SupplierConnectorImportRun importRun)
    {
        if (dbContext is DbContext efDbContext && efDbContext.Entry(importRun).State == EntityState.Detached)
        {
            dbContext.SupplierConnectorImportRuns.Attach(importRun);
        }
    }

    private void ClearChangeTracker()
    {
        if (dbContext is DbContext efDbContext)
        {
            efDbContext.ChangeTracker.Clear();
        }
    }

    private static void AddGlobalProductLookup(
        IDictionary<string, GlobalProduct> normalizedLookup,
        IDictionary<string, GlobalProduct> manufacturerLookup,
        GlobalProduct globalProduct)
    {
        if (!string.IsNullOrWhiteSpace(globalProduct.NormalizedManufacturerPartNumber))
        {
            normalizedLookup.TryAdd(GlobalProductKey(globalProduct.Brand, globalProduct.NormalizedManufacturerPartNumber), globalProduct);
        }

        if (!string.IsNullOrWhiteSpace(globalProduct.ManufacturerPartNumber))
        {
            manufacturerLookup.TryAdd(GlobalProductKey(globalProduct.Brand, globalProduct.ManufacturerPartNumber), globalProduct);
        }
    }

    private static GlobalProduct? FindGlobalProductInBatch(
        WpsImportItem item,
        IReadOnlyDictionary<string, GlobalProduct> normalizedLookup,
        IReadOnlyDictionary<string, GlobalProduct> manufacturerLookup)
    {
        if (!string.IsNullOrWhiteSpace(item.VendorNumber) &&
            manufacturerLookup.TryGetValue(GlobalProductKey(item.Brand, item.VendorNumber), out var manufacturerMatch))
        {
            return manufacturerMatch;
        }

        return !string.IsNullOrWhiteSpace(item.NormalizedManufacturerPartNumber) &&
            normalizedLookup.TryGetValue(GlobalProductKey(item.Brand, item.NormalizedManufacturerPartNumber), out var normalizedMatch)
            ? normalizedMatch
            : null;
    }

    private static string GlobalProductKey(string brand, string partNumber)
    {
        return $"{brand}\u001f{partNumber}";
    }

    private static bool CanUseGlobalProductForExactKey(GlobalProduct globalProduct, string brand, string? manufacturerPartNumber)
    {
        if (string.IsNullOrWhiteSpace(manufacturerPartNumber) ||
            string.IsNullOrWhiteSpace(globalProduct.ManufacturerPartNumber))
        {
            return true;
        }

        return string.Equals(globalProduct.Brand, brand, StringComparison.Ordinal) &&
            string.Equals(globalProduct.ManufacturerPartNumber, manufacturerPartNumber, StringComparison.Ordinal);
    }

    private static int? ReadMaxItems(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(parametersJson);
        if (document.RootElement.TryGetProperty("ImportMode", out var importMode) &&
            importMode.ValueKind == JsonValueKind.String &&
            IsUncappedManualImportMode(importMode.GetString()))
        {
            return null;
        }

        return document.RootElement.TryGetProperty("MaxItems", out var maxItems) && maxItems.ValueKind == JsonValueKind.Number
            ? maxItems.GetInt32()
            : null;
    }

    private static bool IsUncappedManualImportMode(string? importMode)
    {
        return string.Equals(importMode, "Full", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(importMode, "Delta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(importMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase);
    }

    private static string RequiredString(JsonElement item, string propertyName)
    {
        return StringValue(item, propertyName) ?? throw new InvalidOperationException($"WPS master item is missing required field '{propertyName}'.");
    }

    private static string? StringValue(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => Clean(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => Clean(value.GetRawText())
        };
    }

    private static decimal? DecimalValue(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? MediaJson(string? primaryImage)
    {
        return string.IsNullOrWhiteSpace(primaryImage)
            ? null
            : JsonSerializer.Serialize(new[] { new { url = primaryImage, isPrimary = true } });
    }

    private static string SpecificationsJson(JsonElement item)
    {
        return JsonSerializer.Serialize(new
        {
            hasMapPolicy = StringValue(item, "has_map_policy"),
            dropShipEligible = StringValue(item, "drop_ship_eligible"),
            dropShipFee = StringValue(item, "drop_ship_fee"),
            countryOfOriginCode = StringValue(item, "country_of_origin_code"),
            countryOfOriginName = StringValue(item, "country_of_origin_name"),
            supersededSku = StringValue(item, "superseded_sku"),
            productFeatures = StringValue(item, "product_features"),
            catalogGroupDescription = StringValue(item, "cataloggroup_description"),
            catalogs = new
            {
                street = StringValue(item, "street_catalog"),
                offroad = StringValue(item, "offroad_catalog"),
                snow = StringValue(item, "snow_catalog"),
                atv = StringValue(item, "atv_catalog"),
                watercraft = StringValue(item, "watercraft_catalog"),
                bicycle = StringValue(item, "bicycle_catalog"),
                flyRacing = StringValue(item, "flyracing_catalog"),
                harddrive = StringValue(item, "harddrive_catalog"),
                apparel = StringValue(item, "apparel_catalog")
            },
            carb = StringValue(item, "carb"),
            prop65Code = StringValue(item, "prop_65_code"),
            prop65Detail = StringValue(item, "prop_65_detail")
        });
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record WpsImportItem(
        string Sku,
        string Brand,
        string? VendorNumber,
        string? NormalizedManufacturerPartNumber,
        string? Upc,
        string Name,
        string? ProductName,
        string? ProductDescription,
        string? CatalogGroupDescription,
        string? ProductType,
        string Status,
        decimal? Length,
        decimal? Width,
        decimal? Height,
        decimal? Weight,
        string? ImagesJson,
        string SpecificationsJson,
        string SourceDataJson,
        decimal? Msrp,
        decimal? Map)
    {
        public static WpsImportItem From(JsonElement item)
        {
            var sku = RequiredString(item, "sku");
            var vendorNumber = StringValue(item, "vendor_number");
            var primaryImage = StringValue(item, "primary_item_image");

            return new WpsImportItem(
                sku,
                StringValue(item, "brand") ?? "Unknown",
                vendorNumber,
                ProductMatchingService.NormalizeManufacturerPartNumber(vendorNumber),
                StringValue(item, "upc"),
                StringValue(item, "name") ?? sku,
                StringValue(item, "product_name"),
                StringValue(item, "product_description"),
                StringValue(item, "cataloggroup_description"),
                StringValue(item, "product_type"),
                StringValue(item, "status") ?? "Unknown",
                DecimalValue(item, "length"),
                DecimalValue(item, "width"),
                DecimalValue(item, "height"),
                DecimalValue(item, "weight"),
                MediaJson(primaryImage),
                WpsMasterItemListImportService.SpecificationsJson(item),
                item.GetRawText(),
                DecimalValue(item, "list_price"),
                DecimalValue(item, "mapp_price"));
        }
    }

    private sealed class ImportCounters
    {
        public int Processed { get; set; }
        public int CreatedGlobalProducts { get; set; }
        public int UpdatedGlobalProducts { get; set; }
        public int CreatedSupplierProducts { get; set; }
        public int UpdatedSupplierProducts { get; set; }
        public int UpsertedPrices { get; set; }
    }
}
