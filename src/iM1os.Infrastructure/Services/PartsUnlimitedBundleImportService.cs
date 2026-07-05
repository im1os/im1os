using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class PartsUnlimitedBundleImportService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<PartsUnlimitedBundleImportService> logger) : IPartsUnlimitedBundleImportService, IPartsUnlimitedBrandImageImportService
{
    private const int BundleWriteBatchSize = 1000;
    private const int BundleCacheRetentionDays = 3;
    private const int BrandFileCacheRetentionDays = 3;
    private const string SupplierCode = "PU";
    private const string DefaultBaseApiUrl = "https://api.parts-unlimited.com/api";
    private const string DefaultBundlePath = "/v1/parts/bundle";
    private const string DealerPortalBaseUrl = "https://dealer.parts-unlimited.com";
    private const string DealerPortalBrandExportFileName = "Brand_Parts_Info_Export";
    private const string MediaCdnBaseUrl = "https://asset.parts-unlimited.com/media/edge";
    private const string OptionalColumns = "UPC_CODE,BRAND_NAME,COUNTRY_OF_ORIGIN,COMMODITY_CODE,PRODUCT_CODE,DRAG_PART,WEIGHT,CLOSEOUT_CATALOG_INDICATOR,LAST_CATALOG,RACE_ONLY";
    private const string CatalogColumns = "STREET,FATBOOK,ATV,OFFROAD,SNOW,WATERCRAFT,STREET_MIDYEAR,FATBOOK_MIDYEAR,HELMET_AND_APPAREL,TIRE,OLDBOOK,OLDBOOK_MIDYEAR,BICYCLE";

    public async Task<PartsUnlimitedBundleImportResult> ImportAsync(PartsUnlimitedBundleImportRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleAsync(x => x.Id == importRun.SupplierConnectorConfigurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);

        if (!string.Equals(supplier.Code, SupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Import run {request.ImportRunId} is not a Parts Unlimited import run.");
        }

        var apiKey = Clean(configuration.ApiKey) ?? Clean(Environment.GetEnvironmentVariable("PARTS_UNLIMITED_API_KEY"));
        if (apiKey is null)
        {
            throw new InvalidOperationException("Parts Unlimited API key is required.");
        }

        var maxItems = request.MaxItems ?? ReadMaxItems(importRun.ParametersJson);
        var now = dateTimeProvider.UtcNow;
        var effectiveDate = DateOnly.FromDateTime(now.UtcDateTime);

        importRun.Status = "Running";
        importRun.StartedAtUtc = now;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = maxItems;
        importRun.Message = maxItems is null
            ? "Parts Unlimited bundle import started."
            : $"Parts Unlimited bundle import started. Processing first {maxItems.Value} parts.";
        await dbContext.SaveChangesAsync(cancellationToken);

        string? bundlePath = null;
        var deleteBundlePathAfterImport = false;
        try
        {
            var bundle = await GetBundleAsync(configuration, apiKey, now, cancellationToken);
            bundlePath = bundle.Path;
            deleteBundlePathAfterImport = bundle.DeleteAfterImport;

            var counters = await ImportBundleAsync(
                bundlePath,
                supplier.Id,
                maxItems,
                effectiveDate,
                now,
                importRun,
                cancellationToken);

            TrackImportRun(importRun);
            await dbContext.SaveChangesAsync(cancellationToken);

            TrackImportRun(importRun);
            importRun.Status = "Completed";
            importRun.CompletedAtUtc = dateTimeProvider.UtcNow;
            importRun.ProgressProcessed = counters.Processed;
            importRun.ProgressTotal = counters.Processed;
            importRun.Message = $"Parts Unlimited bundle import completed. Processed {counters.Processed} parts, created {counters.CreatedGlobalProducts} global products, created {counters.CreatedSupplierProducts} supplier products.";
            await dbContext.SaveChangesAsync(cancellationToken);

            return new PartsUnlimitedBundleImportResult(
                importRun.Id,
                counters.Processed,
                counters.CreatedGlobalProducts,
                counters.UpdatedGlobalProducts,
                counters.CreatedSupplierProducts,
                counters.UpdatedSupplierProducts,
                counters.UpsertedPrices,
                0,
                0,
                0,
                0);
        }
        finally
        {
            if (deleteBundlePathAfterImport && !string.IsNullOrWhiteSpace(bundlePath))
            {
                TryDeleteDownloadDirectory(bundlePath);
            }
        }
    }

    public async Task<PartsUnlimitedBrandImageImportResult> ImportAsync(PartsUnlimitedBrandImageImportRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleAsync(x => x.Id == importRun.SupplierConnectorConfigurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);

        if (!string.Equals(supplier.Code, SupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Import run {request.ImportRunId} is not a Parts Unlimited brand image import run.");
        }

        var apiKey = Clean(configuration.ApiKey) ?? Clean(Environment.GetEnvironmentVariable("PARTS_UNLIMITED_API_KEY"));
        if (apiKey is null)
        {
            throw new InvalidOperationException("Parts Unlimited API key is required.");
        }

        importRun.Status = "Running";
        importRun.StartedAtUtc = dateTimeProvider.UtcNow;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = request.MaxFiles;
        importRun.Message = request.MaxFiles is null
            ? "Parts Unlimited brand image import started."
            : $"Parts Unlimited brand image import started. Processing first {request.MaxFiles.Value:N0} brand files.";
        await dbContext.SaveChangesAsync(cancellationToken);

        var counters = await ImportBrandFilesAsync(
            supplier.Id,
            configuration,
            apiKey,
            importRun,
            request.MaxFiles,
            cancellationToken);

        importRun.Status = "Completed";
        importRun.CompletedAtUtc = dateTimeProvider.UtcNow;
        importRun.ProgressProcessed = counters.BrandFilesProcessed;
        importRun.ProgressTotal = request.MaxFiles ?? counters.BrandFilesProcessed;
        importRun.Message = $"Parts Unlimited brand image import completed. Brand files processed {counters.BrandFilesProcessed}, image rows {counters.BrandImageRowsProcessed}, images updated {counters.BrandImagesUpdated}, unmatched {counters.BrandImageRowsUnmatched}.";
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PartsUnlimitedBrandImageImportResult(
            importRun.Id,
            counters.BrandFilesProcessed,
            counters.BrandImageRowsProcessed,
            counters.BrandImagesUpdated,
            counters.BrandImageRowsUnmatched);
    }

    private async Task<BundleDownload> GetBundleAsync(SupplierConnectorConfiguration configuration, string apiKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requestUri = BuildBundleUri(configuration);
        var cachePath = BundleCachePath(requestUri, now);
        if (File.Exists(cachePath) && HasPkSignature(cachePath))
        {
            logger.LogInformation("Using cached Parts Unlimited bundle from {CachePath}.", cachePath);
            return new BundleDownload(cachePath, DeleteAfterImport: false);
        }

        CleanupOldBundleCache(now);

        var client = httpClientFactory.CreateClient("PartsUnlimitedApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Parts Unlimited bundle request failed with HTTP {(int)response.StatusCode}: {TrimForMessage(body)}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var tempPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        await using (var output = File.Create(tempPath))
        {
            await response.Content.CopyToAsync(output, cancellationToken);
        }

        if (!HasPkSignature(tempPath))
        {
            throw new InvalidOperationException("Parts Unlimited bundle response was not a ZIP file.");
        }

        if (File.Exists(cachePath))
        {
            File.Delete(cachePath);
        }

        File.Move(tempPath, cachePath);
        logger.LogInformation("Downloaded and cached Parts Unlimited bundle at {CachePath}.", cachePath);
        return new BundleDownload(cachePath, DeleteAfterImport: false);
    }

    private static string BundleCachePath(Uri requestUri, DateTimeOffset now)
    {
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestUri.AbsoluteUri)));
        return Path.Combine(
            Path.GetTempPath(),
            "im1os-parts-unlimited",
            "bundle-cache",
            now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            $"{cacheKey}.zip");
    }

    private static void CleanupOldBundleCache(DateTimeOffset now)
    {
        try
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "im1os-parts-unlimited", "bundle-cache");
            if (!Directory.Exists(cacheRoot))
            {
                return;
            }

            var oldestRetainedDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-BundleCacheRetentionDays);
            foreach (var directory in Directory.EnumerateDirectories(cacheRoot))
            {
                var directoryName = Path.GetFileName(directory);
                if (DateOnly.TryParseExact(directoryName, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cacheDate) &&
                    cacheDate < oldestRetainedDate)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
        catch
        {
            // Best-effort cache cleanup only.
        }
    }

    private static Uri BuildBundleUri(SupplierConnectorConfiguration configuration)
    {
        var source = Clean(configuration.MasterFileUrl);
        var sourceIsUnsupportedAbsoluteUri = Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri) && !IsHttpUri(absoluteUri);
        if (absoluteUri is not null && IsHttpUri(absoluteUri))
        {
            return absoluteUri;
        }

        var baseApiUrl = Clean(configuration.BaseApiUrl) ?? DefaultBaseApiUrl;
        if (!Uri.TryCreate(baseApiUrl, UriKind.Absolute, out var baseUri) || !IsHttpUri(baseUri))
        {
            baseApiUrl = DefaultBaseApiUrl;
        }

        var relativePath = sourceIsUnsupportedAbsoluteUri ? DefaultBundlePath : Clean(source) ?? DefaultBundlePath;
        var builder = new UriBuilder($"{baseApiUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}");
        if (string.IsNullOrWhiteSpace(builder.Query))
        {
            builder.Query = $"includeOptionalColumns={Uri.EscapeDataString(OptionalColumns)}&includeCatalogs={Uri.EscapeDataString(CatalogColumns)}";
        }

        return builder.Uri;
    }

    private static bool IsHttpUri(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ImportCounters> ImportBundleAsync(
        string zipPath,
        Guid supplierId,
        int? maxItems,
        DateOnly effectiveDate,
        DateTimeOffset now,
        SupplierConnectorImportRun importRun,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var partInfoEntry = archive.Entries.FirstOrDefault(x => x.Length > 0 && x.Name.Equals("PartInfo.csv", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(x => x.Length > 0 && x.Name.Contains("PartInfo", StringComparison.OrdinalIgnoreCase));
        if (partInfoEntry is null)
        {
            throw new InvalidOperationException("Parts Unlimited bundle did not contain PartInfo.csv.");
        }

        var priceEntry = archive.Entries.FirstOrDefault(x =>
            x.Length > 0 &&
            x.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            x.Name.Contains("PartPrices", StringComparison.OrdinalIgnoreCase));

        var priceRows = await ReadPriceRowsAsync(priceEntry, cancellationToken);
        importRun.ProgressTotal = maxItems;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ImportPartRowsAsync(
            supplierId,
            partInfoEntry,
            maxItems,
            priceRows,
            effectiveDate,
            now,
            importRun,
            cancellationToken);
    }

    private static async Task<Dictionary<string, IReadOnlyDictionary<string, string?>>> ReadPriceRowsAsync(ZipArchiveEntry? entry, CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);
        if (entry is null)
        {
            return rows;
        }

        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headers = await ReadCsvRecordAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException($"Parts Unlimited bundle file '{entry.Name}' is missing a header row.");
        var delimiter = InferDelimiter(headers);
        if (delimiter != ',')
        {
            headers = SplitRecord(string.Join(',', headers), delimiter);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = await ReadCsvRecordAsync(reader, cancellationToken, delimiter);
            if (values is null)
            {
                break;
            }

            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var row = BuildRow(headers, values);
            var partNumber = FirstField(row, "partNumber", "Part Number");
            if (partNumber is not null && !rows.ContainsKey(partNumber))
            {
                rows.Add(partNumber, row);
            }
        }

        return rows;
    }

    private async Task<ImportCounters> ImportPartRowsAsync(
        Guid supplierId,
        ZipArchiveEntry partInfoEntry,
        int? maxItems,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> priceRows,
        DateOnly effectiveDate,
        DateTimeOffset now,
        SupplierConnectorImportRun importRun,
        CancellationToken cancellationToken)
    {
        var counters = new ImportCounters();
        await using var entryStream = partInfoEntry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headers = await ReadCsvRecordAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException($"Parts Unlimited bundle file '{partInfoEntry.Name}' is missing a header row.");
        var delimiter = InferDelimiter(headers);
        if (delimiter != ',')
        {
            headers = SplitRecord(string.Join(',', headers), delimiter);
        }

        while (maxItems is null || counters.Processed < maxItems.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = new List<PartImportRow>(BundleWriteBatchSize);
            while (batch.Count < BundleWriteBatchSize && (maxItems is null || counters.Processed + batch.Count < maxItems.Value))
            {
                var values = await ReadCsvRecordAsync(reader, cancellationToken, delimiter);
                if (values is null)
                {
                    break;
                }

                if (values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                batch.Add(PartImportRow.From(BuildRow(headers, values)));
            }

            if (batch.Count == 0)
            {
                break;
            }

            await UpsertPartBatchAsync(supplierId, batch, priceRows, effectiveDate, now, counters, cancellationToken);
            await SaveBundleBatchAsync(importRun, counters.Processed, cancellationToken);
            logger.LogInformation("Imported {Count} Parts Unlimited bundle rows for run {ImportRunId}.", counters.Processed, importRun.Id);
        }

        importRun.ProgressProcessed = counters.Processed;
        return counters;
    }

    private async Task UpsertPartBatchAsync(
        Guid supplierId,
        IReadOnlyCollection<PartImportRow> rows,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> priceRows,
        DateOnly effectiveDate,
        DateTimeOffset now,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var skus = rows
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

        var normalizedPartNumbers = rows
            .Select(x => x.NormalizedManufacturerPartNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var manufacturerPartNumbers = rows
            .Select(x => x.ManufacturerPartNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var globalCandidates = normalizedPartNumbers.Length == 0 && manufacturerPartNumbers.Length == 0
            ? new List<GlobalProduct>()
            : await dbContext.GlobalProducts
                .Where(x =>
                    (x.NormalizedManufacturerPartNumber != null && normalizedPartNumbers.Contains(x.NormalizedManufacturerPartNumber)) ||
                    (x.ManufacturerPartNumber != null && manufacturerPartNumbers.Contains(x.ManufacturerPartNumber)))
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

        foreach (var row in rows)
        {
            UpsertPart(row);
        }

        void UpsertPart(PartImportRow row)
        {
            var supplierProductExists = supplierProductsBySku.TryGetValue(row.Sku, out var supplierProduct);
            var currentGlobalProduct = supplierProductExists
                ? globalProductsById[supplierProduct!.GlobalProductId]
                : null;
            var globalProduct = FindGlobalProductInBatch(row, globalProductsByNormalizedKey, globalProductsByManufacturerKey);
            if (globalProduct is not null && !CanUseGlobalProductForExactKey(globalProduct, row.Brand, row.ManufacturerPartNumber))
            {
                globalProduct = null;
            }

            if (globalProduct is null &&
                currentGlobalProduct is not null &&
                CanUseGlobalProductForExactKey(currentGlobalProduct, row.Brand, row.ManufacturerPartNumber))
            {
                globalProduct = currentGlobalProduct;
            }

            if (globalProduct is null)
            {
                globalProduct = new GlobalProduct
                {
                    Brand = row.Brand,
                    Manufacturer = row.Brand,
                    ManufacturerPartNumber = row.ManufacturerPartNumber,
                    NormalizedManufacturerPartNumber = row.NormalizedManufacturerPartNumber,
                    Description = row.Description,
                    LongDescription = FirstField(row.SourceRow, "Notes"),
                    Category = row.Category,
                    Upc = row.Upc,
                    Weight = DecimalField(row.SourceRow, "WEIGHT", "Weight"),
                    SpecificationsJson = SpecificationsJson(row.SourceRow),
                    Status = IsInactiveStatus(row.Status) ? "Inactive" : "Active",
                    IsActive = !IsInactiveStatus(row.Status)
                };
                dbContext.GlobalProducts.Add(globalProduct);
                AddGlobalProductLookup(globalProductsByNormalizedKey, globalProductsByManufacturerKey, globalProduct);
                counters.CreatedGlobalProducts++;
            }
            else
            {
                globalProduct.Brand = row.Brand;
                globalProduct.Manufacturer ??= row.Brand;
                globalProduct.ManufacturerPartNumber ??= row.ManufacturerPartNumber;
                globalProduct.NormalizedManufacturerPartNumber ??= row.NormalizedManufacturerPartNumber;
                globalProduct.Description = row.Description;
                globalProduct.LongDescription = FirstField(row.SourceRow, "Notes") ?? globalProduct.LongDescription;
                globalProduct.Category = row.Category ?? globalProduct.Category;
                globalProduct.Upc ??= row.Upc;
                globalProduct.Weight = DecimalField(row.SourceRow, "WEIGHT", "Weight") ?? globalProduct.Weight;
                globalProduct.SpecificationsJson = SpecificationsJson(row.SourceRow);
                globalProduct.Status = IsInactiveStatus(row.Status) ? "Inactive" : "Active";
                globalProduct.IsActive = !IsInactiveStatus(row.Status);
                AddGlobalProductLookup(globalProductsByNormalizedKey, globalProductsByManufacturerKey, globalProduct);
                counters.UpdatedGlobalProducts++;
            }

            CatalogTireParser.Apply(
                globalProduct,
                row.Brand,
                row.Category,
                row.Description,
                FirstField(row.SourceRow, "Notes"),
                SpecificationsJson(row.SourceRow));

            if (supplierProduct is null)
            {
                supplierProduct = new SupplierProduct
                {
                    SupplierId = supplierId,
                    GlobalProductId = globalProduct.Id,
                    SupplierSku = row.Sku,
                    SupplierStatus = row.Status
                };
                dbContext.SupplierProducts.Add(supplierProduct);
                supplierProductsBySku[row.Sku] = supplierProduct;
                counters.CreatedSupplierProducts++;
            }
            else
            {
                counters.UpdatedSupplierProducts++;
            }

            supplierProduct.GlobalProductId = globalProduct.Id;
            supplierProduct.SupplierDescription = row.Description;
            supplierProduct.SupplierPartNumber = row.SupplierPartNumber;
            supplierProduct.ManufacturerPartNumber = row.ManufacturerPartNumber;
            supplierProduct.NormalizedManufacturerPartNumber = row.NormalizedManufacturerPartNumber;
            supplierProduct.SupplierStatus = row.Status;
            supplierProduct.SourceSupplierProductId = row.Sku;
            supplierProduct.SourceDataJson = JsonSerializer.Serialize(new { partInfo = row.SourceRow });
            supplierProduct.LastSyncedAtUtc = now;

            priceRows.TryGetValue(row.Sku, out var priceRow);
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

            price.Msrp = priceRow is null ? null : DecimalField(priceRow, "retailPrice", "Retail Price", "retail");
            price.Map = null;
            price.DealerCost = 0m;
            price.LastUpdated = now;
            counters.UpsertedPrices++;
            counters.Processed++;
        }
    }

    private async Task SaveBundleBatchAsync(SupplierConnectorImportRun importRun, int processed, CancellationToken cancellationToken)
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
        PartImportRow row,
        IReadOnlyDictionary<string, GlobalProduct> normalizedLookup,
        IReadOnlyDictionary<string, GlobalProduct> manufacturerLookup)
    {
        if (!string.IsNullOrWhiteSpace(row.ManufacturerPartNumber) &&
            manufacturerLookup.TryGetValue(GlobalProductKey(row.Brand, row.ManufacturerPartNumber), out var manufacturerMatch))
        {
            return manufacturerMatch;
        }

        return !string.IsNullOrWhiteSpace(row.NormalizedManufacturerPartNumber) &&
            normalizedLookup.TryGetValue(GlobalProductKey(row.Brand, row.NormalizedManufacturerPartNumber), out var normalizedMatch)
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

    private async Task<BrandImportCounters> ImportBrandFilesAsync(
        Guid supplierId,
        SupplierConnectorConfiguration configuration,
        string apiKey,
        SupplierConnectorImportRun importRun,
        int? maxFiles,
        CancellationToken cancellationToken)
    {
        var parameters = PartsUnlimitedBundleRunParameters.FromJson(importRun.ParametersJson);
        if (!parameters.ImportBrandFiles)
        {
            return new BrandImportCounters();
        }

        var brandFileUrls = ResolveBrandFileUrls(configuration, parameters).ToList();
        var maxBrandFiles = maxFiles ?? parameters.BrandFileMaxFiles;
        if (maxBrandFiles is > 0)
        {
            brandFileUrls = brandFileUrls.Take(maxBrandFiles.Value).ToList();
        }

        if (brandFileUrls.Count == 0)
        {
            return new BrandImportCounters();
        }

        var counters = new BrandImportCounters();
        HttpClient? dealerPortalClient = null;
        try
        {
            if (brandFileUrls.Any(IsDealerPortalBrandExportSource))
            {
                dealerPortalClient = await CreateDealerPortalClientAsync(configuration, cancellationToken);
            }

            foreach (var brandFileUrl in brandFileUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                importRun.ProgressProcessed = counters.BrandFilesProcessed;
                importRun.ProgressTotal = brandFileUrls.Count;
                importRun.Message = $"Processing Parts Unlimited brand image file {counters.BrandFilesProcessed + 1:N0} of {brandFileUrls.Count:N0}. Images updated {counters.BrandImagesUpdated:N0}, unmatched {counters.BrandImageRowsUnmatched:N0}.";
                await dbContext.SaveChangesAsync(cancellationToken);

                var brandFile = await DownloadBrandFileAsync(configuration, apiKey, brandFileUrl, dealerPortalClient, importRun.Id, counters.BrandFilesProcessed + 1, cancellationToken);
                try
                {
                    await ProcessBrandFileAsync(supplierId, brandFile.Path, counters, cancellationToken);
                    counters.BrandFilesProcessed++;
                    importRun.ProgressProcessed = counters.BrandFilesProcessed;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("Processed Parts Unlimited brand image file {BrandFileUrl} for run {ImportRunId}.", brandFileUrl, importRun.Id);
                }
                finally
                {
                    if (brandFile.DeleteAfterImport)
                    {
                        TryDeleteDownloadDirectory(brandFile.Path);
                    }
                }
            }
        }
        finally
        {
            dealerPortalClient?.Dispose();
        }

        return counters;
    }

    private async Task<BrandFileDownload> DownloadBrandFileAsync(
        SupplierConnectorConfiguration configuration,
        string apiKey,
        string source,
        HttpClient? dealerPortalClient,
        Guid importRunId,
        int sequence,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildSupplementalUri(configuration, source);
        var cachePath = BrandFileCachePath(requestUri, dateTimeProvider.UtcNow);
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
        {
            logger.LogInformation("Using cached Parts Unlimited brand file from {CachePath}.", cachePath);
            return new BrandFileDownload(cachePath, DeleteAfterImport: false);
        }

        CleanupOldBrandFileCache(dateTimeProvider.UtcNow);

        var useDealerPortal = IsDealerPortalBrandExportUri(requestUri);
        var client = useDealerPortal
            ? dealerPortalClient ?? throw new InvalidOperationException("Parts Unlimited dealer portal session is required for cached brand file downloads.")
            : httpClientFactory.CreateClient("PartsUnlimitedApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (useDealerPortal)
        {
            request.Headers.TryAddWithoutValidation("Accept", "application/zip,application/octet-stream,*/*");
            request.Headers.TryAddWithoutValidation("Origin", DealerPortalBaseUrl);
            request.Headers.TryAddWithoutValidation("Referer", $"{DealerPortalBaseUrl}/reptools/brand-report");
        }
        else
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var tempPath = $"{cachePath}.{importRunId:N}.{sequence.ToString(CultureInfo.InvariantCulture)}.tmp";
        await using (var output = File.Create(tempPath))
        {
            await response.Content.CopyToAsync(output, cancellationToken);
        }

        if (File.Exists(cachePath))
        {
            File.Delete(cachePath);
        }

        File.Move(tempPath, cachePath);
        logger.LogInformation("Downloaded and cached Parts Unlimited brand file at {CachePath}.", cachePath);
        return new BrandFileDownload(cachePath, DeleteAfterImport: false);
    }

    private static string BrandFileCachePath(Uri requestUri, DateTimeOffset now)
    {
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestUri.AbsoluteUri)));
        var extension = BrandFileCacheExtension(requestUri);
        return Path.Combine(
            Path.GetTempPath(),
            "im1os-parts-unlimited",
            "brand-file-cache",
            now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            $"{cacheKey}{extension}");
    }

    private static string BrandFileCacheExtension(Uri requestUri)
    {
        if (IsDealerPortalBrandExportUri(requestUri))
        {
            return ".zip";
        }

        var extension = Path.GetExtension(requestUri.LocalPath);
        return string.IsNullOrWhiteSpace(extension) ? ".zip" : extension;
    }

    private static void CleanupOldBrandFileCache(DateTimeOffset now)
    {
        try
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "im1os-parts-unlimited", "brand-file-cache");
            if (!Directory.Exists(cacheRoot))
            {
                return;
            }

            var oldestRetainedDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-BrandFileCacheRetentionDays);
            foreach (var directory in Directory.EnumerateDirectories(cacheRoot))
            {
                var directoryName = Path.GetFileName(directory);
                if (DateOnly.TryParseExact(directoryName, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cacheDate) &&
                    cacheDate < oldestRetainedDate)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
        catch
        {
            // Best-effort cache cleanup only.
        }
    }

    private static IReadOnlyCollection<string> ResolveBrandFileUrls(SupplierConnectorConfiguration configuration, PartsUnlimitedBundleRunParameters parameters)
    {
        var configuredUrls = SplitBrandFileUrls(parameters.BrandFileUrls).ToList();
        if (configuredUrls.Count > 0)
        {
            return configuredUrls;
        }

        var options = PartsUnlimitedConnectorOptions.FromConfiguration(configuration);
        return options.CachedBrands
            .Where(x => !string.IsNullOrWhiteSpace(x.BrandId))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => BuildDealerPortalBrandExportUrl(x.BrandId))
            .ToList();
    }

    private static string BuildDealerPortalBrandExportUrl(string brandId)
    {
        return $"{DealerPortalBaseUrl}/api/parts/export/{Uri.EscapeDataString(brandId)}?exportType=PARTS_ONLY&exportFileName={DealerPortalBrandExportFileName}";
    }

    private static bool IsDealerPortalBrandExportSource(string source)
    {
        return Uri.TryCreate(source, UriKind.Absolute, out var uri) && IsDealerPortalBrandExportUri(uri);
    }

    private static bool IsDealerPortalBrandExportUri(Uri uri)
    {
        return uri.Host.Equals("dealer.parts-unlimited.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith("/api/parts/export/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpClient> CreateDealerPortalClientAsync(SupplierConnectorConfiguration configuration, CancellationToken cancellationToken)
    {
        var options = PartsUnlimitedConnectorOptions.FromConfiguration(configuration);
        var missingFields = RequiredPartsUnlimitedDealerPortalFields(options).ToArray();
        if (missingFields.Length > 0)
        {
            throw new InvalidOperationException($"Parts Unlimited dealer portal credentials are required for cached brand file downloads. Missing: {string.Join(", ", missingFields)}.");
        }

        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var loginRequest = new HttpRequestMessage(HttpMethod.Put, $"{DealerPortalBaseUrl}/api/login?t={timestamp}");
            loginRequest.Headers.TryAddWithoutValidation("Origin", DealerPortalBaseUrl);
            loginRequest.Headers.TryAddWithoutValidation("Referer", $"{DealerPortalBaseUrl}/login");
            loginRequest.Content = JsonContent(new
            {
                username = options.DealerPortalUsername,
                password = options.DealerPortalPassword,
                dealerCode = options.DealerPortalDealerCode
            });

            using var loginResponse = await client.SendAsync(loginRequest, cancellationToken);
            if (!loginResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Parts Unlimited dealer portal login failed.", null, loginResponse.StatusCode);
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static IEnumerable<string> RequiredPartsUnlimitedDealerPortalFields(PartsUnlimitedConnectorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DealerPortalUsername))
        {
            yield return "Dealer Portal User ID";
        }

        if (string.IsNullOrWhiteSpace(options.DealerPortalPassword))
        {
            yield return "Dealer Portal Password";
        }

        if (string.IsNullOrWhiteSpace(options.DealerPortalDealerCode))
        {
            yield return "Dealer Number";
        }
    }

    private static StringContent JsonContent<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }

    private async Task ProcessBrandFileAsync(Guid supplierId, string path, BrandImportCounters counters, CancellationToken cancellationToken)
    {
        if (HasPkSignature(path))
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries.Where(x => x.Length > 0).OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsBrandDataEntry(entry.FullName))
                {
                    continue;
                }

                using var entryStream = entry.Open();
                await ProcessBrandDataStreamAsync(supplierId, entryStream, entry.FullName, counters, cancellationToken);
            }

            return;
        }

        await using var stream = File.OpenRead(path);
        await ProcessBrandDataStreamAsync(supplierId, stream, Path.GetFileName(path), counters, cancellationToken);
    }

    private async Task ProcessBrandDataStreamAsync(Guid supplierId, Stream stream, string sourceName, BrandImportCounters counters, CancellationToken cancellationToken)
    {
        if (sourceName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessBrandXmlAsync(supplierId, stream, counters, cancellationToken);
        }
    }

    private async Task ProcessBrandXmlAsync(Guid supplierId, Stream stream, BrandImportCounters counters, CancellationToken cancellationToken)
    {
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var imageElements = document
            .Descendants()
            .Where(x => x.Name.LocalName.Equals("partImage", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var records = new List<BrandImageImportRecord>();

        foreach (var imageElement in imageElements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recordElement = imageElement.Parent ?? imageElement;
            var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in recordElement.Attributes())
            {
                record[attribute.Name.LocalName] = Clean(attribute.Value);
            }

            foreach (var element in recordElement.DescendantsAndSelf().Where(x => !x.HasElements))
            {
                record[element.Name.LocalName] = Clean(element.Value);
            }

            var imageValue = FirstField(record, "partImage", "PartImage", "image", "Image");
            var images = DecodePartImageUrls(imageValue).ToList();
            if (images.Count == 0)
            {
                continue;
            }

            counters.BrandImageRowsProcessed++;
            records.Add(new BrandImageImportRecord(
                PartNumberCandidates(record).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                JsonSerializer.Serialize(images)));
        }

        await UpsertBrandImageRecordsAsync(supplierId, records, counters, cancellationToken);
    }

    private async Task UpsertBrandImageRecordsAsync(
        Guid supplierId,
        IReadOnlyCollection<BrandImageImportRecord> records,
        BrandImportCounters counters,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return;
        }

        var candidates = records
            .SelectMany(x => x.PartNumberCandidates)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var supplierProducts = new List<SupplierProduct>();
        foreach (var candidateBatch in candidates.Chunk(5000))
        {
            var batch = candidateBatch;
            supplierProducts.AddRange(await dbContext.SupplierProducts
                .Where(x =>
                    x.SupplierId == supplierId &&
                    (batch.Contains(x.SupplierSku) ||
                        (x.SupplierPartNumber != null && batch.Contains(x.SupplierPartNumber)) ||
                        (x.ManufacturerPartNumber != null && batch.Contains(x.ManufacturerPartNumber))))
                .OrderBy(x => x.SupplierSku)
                .ToListAsync(cancellationToken));
        }

        supplierProducts = supplierProducts
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
        var supplierProductsByCandidate = BuildSupplierProductCandidateMap(supplierProducts);
        var globalProductIds = supplierProducts
            .Select(x => x.GlobalProductId)
            .Distinct()
            .ToArray();
        var globalProductsById = globalProductIds.Length == 0
            ? new Dictionary<Guid, GlobalProduct>()
            : await dbContext.GlobalProducts
                .Where(x => globalProductIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var supplierProduct = MatchSupplierProduct(record.PartNumberCandidates, supplierProductsByCandidate);
            if (supplierProduct is null)
            {
                counters.BrandImageRowsUnmatched++;
                continue;
            }

            var updated = false;
            if (!string.Equals(supplierProduct.SupplierImagesJson, record.ImagesJson, StringComparison.Ordinal))
            {
                supplierProduct.SupplierImagesJson = record.ImagesJson;
                updated = true;
            }

            if (globalProductsById.TryGetValue(supplierProduct.GlobalProductId, out var globalProduct) &&
                string.IsNullOrWhiteSpace(globalProduct.ImagesJson))
            {
                globalProduct.ImagesJson = record.ImagesJson;
                updated = true;
            }

            if (updated)
            {
                counters.BrandImagesUpdated++;
            }
        }
    }

    private static IReadOnlyDictionary<string, SupplierProduct> BuildSupplierProductCandidateMap(IEnumerable<SupplierProduct> supplierProducts)
    {
        var productsByCandidate = new Dictionary<string, SupplierProduct>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplierProduct in supplierProducts)
        {
            AddCandidate(supplierProduct.SupplierSku);
            AddCandidate(supplierProduct.SupplierPartNumber);
            AddCandidate(supplierProduct.ManufacturerPartNumber);

            void AddCandidate(string? value)
            {
                if (Clean(value) is { } candidate)
                {
                    productsByCandidate.TryAdd(candidate, supplierProduct);
                }
            }
        }

        return productsByCandidate;
    }

    private static SupplierProduct? MatchSupplierProduct(IEnumerable<string> partNumberCandidates, IReadOnlyDictionary<string, SupplierProduct> supplierProductsByCandidate)
    {
        foreach (var candidate in partNumberCandidates)
        {
            if (supplierProductsByCandidate.TryGetValue(candidate, out var supplierProduct))
            {
                return supplierProduct;
            }
        }

        return null;
    }

    private static Uri BuildSupplementalUri(SupplierConnectorConfiguration configuration, string source)
    {
        var cleanSource = Clean(source) ?? throw new InvalidOperationException("Parts Unlimited brand file URL is required.");
        if (Uri.TryCreate(cleanSource, UriKind.Absolute, out var absoluteUri) && IsHttpUri(absoluteUri))
        {
            return absoluteUri;
        }

        var baseApiUrl = Clean(configuration.BaseApiUrl) ?? DefaultBaseApiUrl;
        if (!Uri.TryCreate(baseApiUrl, UriKind.Absolute, out var baseUri) || !IsHttpUri(baseUri))
        {
            baseApiUrl = DefaultBaseApiUrl;
        }

        return new Uri($"{baseApiUrl.TrimEnd('/')}/{cleanSource.TrimStart('/')}");
    }

    private static IEnumerable<string> SplitBrandFileUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var source in value.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(source))
            {
                yield return source;
            }
        }
    }

    private static bool IsBrandDataEntry(string fileName)
    {
        return fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<PartMediaImage> DecodePartImageUrls(string? partImage)
    {
        var encoded = LastPathSegment(partImage);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            yield break;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(encoded.Replace('-', '+').Replace('_', '/'))));
        }
        catch (FormatException)
        {
            yield break;
        }

        var index = 0;
        foreach (var rawPath in decoded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cleanPath = rawPath.Trim().Trim('"').Trim();
            if (!TryNormalizePartsUnlimitedMediaPath(cleanPath, out var normalizedPath))
            {
                continue;
            }

            yield return new PartMediaImage(
                $"{MediaCdnBaseUrl}/{normalizedPath}.png",
                $"{MediaCdnBaseUrl}/{normalizedPath}.png?x=240&y=240&b=&t=image/jpeg",
                index == 0,
                index + 1);
            index++;
        }
    }

    private static string? LastPathSegment(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return null;
        }

        var queryStart = clean.IndexOf('?', StringComparison.Ordinal);
        if (queryStart >= 0)
        {
            clean = clean[..queryStart];
        }

        var slashIndex = clean.LastIndexOf('/');
        var segment = slashIndex >= 0 ? clean[(slashIndex + 1)..] : clean;
        return Uri.UnescapeDataString(segment);
    }

    private static string PadBase64(string value)
    {
        var remainder = value.Length % 4;
        return remainder == 0 ? value : value.PadRight(value.Length + 4 - remainder, '=');
    }

    private static bool TryNormalizePartsUnlimitedMediaPath(string value, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        var parts = value.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4 || !Guid.TryParse(parts[^1], out var mediaId))
        {
            return false;
        }

        normalizedPath = string.Join('/', parts.Take(parts.Length - 1).Append(mediaId.ToString().ToUpperInvariant()));
        return true;
    }

    private static IEnumerable<string> PartNumberCandidates(IReadOnlyDictionary<string, string?> row)
    {
        var candidateFields = new[]
        {
            "partNumber",
            "PartNumber",
            "part_number",
            "Part Number",
            "punctuatedPartNumber",
            "PunctuatedPartNumber",
            "Punctuated Part Number",
            "vendorPartNumber",
            "VendorPartNumber",
            "Vendor Part Number",
            "sku",
            "SKU"
        };

        foreach (var fieldName in candidateFields)
        {
            var value = FirstField(row, fieldName);
            if (value is not null)
            {
                yield return value;
            }
        }
    }

    private static async Task<string[]?> ReadCsvRecordAsync(StreamReader reader, CancellationToken cancellationToken, char delimiter = ',')
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            return null;
        }

        var record = line;
        while (HasOpenQuote(record))
        {
            var continuation = await reader.ReadLineAsync(cancellationToken);
            if (continuation is null)
            {
                break;
            }

            record += "\n" + continuation;
        }

        return SplitRecord(record, delimiter);
    }

    private static string[] SplitRecord(string record, char delimiter)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < record.Length; index++)
        {
            var character = record[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < record.Length && record[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == delimiter && !inQuotes)
            {
                fields.Add(value.ToString().Trim());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        fields.Add(value.ToString().Trim());
        return fields.ToArray();
    }

    private static bool HasOpenQuote(string value)
    {
        var quoteCount = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '"')
            {
                continue;
            }

            if (index + 1 < value.Length && value[index + 1] == '"')
            {
                index++;
                continue;
            }

            quoteCount++;
        }

        return quoteCount % 2 != 0;
    }

    private static char InferDelimiter(IReadOnlyList<string> headerFields)
    {
        if (headerFields.Count > 1)
        {
            return ',';
        }

        var header = headerFields[0];
        return header.Count(x => x == '\t') > header.Count(x => x == ',') ? '\t' : ',';
    }

    private static IReadOnlyDictionary<string, string?> BuildRow(IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var header = Clean(headers[index])?.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(header) || row.ContainsKey(header))
            {
                continue;
            }

            row[header] = index < values.Count ? Clean(values[index]) : null;
        }

        return row;
    }

    private static string RequiredField(IReadOnlyDictionary<string, string?> row, string fieldName)
    {
        return FirstField(row, fieldName) ?? throw new InvalidOperationException($"Parts Unlimited bundle row is missing required field '{fieldName}'.");
    }

    private static string? FirstField(IReadOnlyDictionary<string, string?> row, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (row.TryGetValue(fieldName, out var value) && Clean(value) is { } clean)
            {
                return clean;
            }
        }

        return null;
    }

    private static decimal? DecimalField(IReadOnlyDictionary<string, string?> row, params string[] fieldNames)
    {
        var value = FirstField(row, fieldNames);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace("$", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string SpecificationsJson(IReadOnlyDictionary<string, string?> row)
    {
        return JsonSerializer.Serialize(new
        {
            punctuatedPartNumber = FirstField(row, "Punctuated Part Number"),
            vendorPunctuatedPartNumber = FirstField(row, "Vendor Punctuated Part Number"),
            hazardousCode = FirstField(row, "Hazardous Code"),
            truckPartOnly = FirstField(row, "Truck Part Only"),
            partAddDate = FirstField(row, "Part Add Date"),
            unitOfMeasure = FirstField(row, "Unit of Measure"),
            noShipToCalifornia = FirstField(row, "No Ship to CA"),
            countryOfOrigin = FirstField(row, "COUNTRY_OF_ORIGIN", "Country of Origin"),
            commodityCode = FirstField(row, "COMMODITY_CODE", "Commodity Code"),
            productCode = FirstField(row, "PRODUCT_CODE", "Product Code"),
            dragPart = FirstField(row, "DRAG_PART", "Drag Part"),
            closeoutCatalogIndicator = FirstField(row, "CLOSEOUT_CATALOG_INDICATOR", "Closeout Catalog Indicator"),
            lastCatalog = FirstField(row, "LAST_CATALOG", "Last Catalog"),
            raceOnly = FirstField(row, "RACE_ONLY", "Race Only"),
            catalogs = row
                .Where(x => IsCatalogColumn(x.Key))
                .ToDictionary(x => x.Key, x => x.Value)
        });
    }

    private static bool IsCatalogColumn(string key)
    {
        return key.Contains("CATALOG", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("STREET", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("FATBOOK", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("ATV", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("OFFROAD", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("SNOW", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("WATERCRAFT", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("BICYCLE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInactiveStatus(string status)
    {
        return status.Equals("D", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("NLA", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Inactive", StringComparison.OrdinalIgnoreCase);
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

    private static string TrimForMessage(string? value)
    {
        var clean = string.IsNullOrWhiteSpace(value)
            ? "No response body returned."
            : value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return clean.Length <= 500 ? clean : $"{clean[..500]}...";
    }

    private static bool HasPkSignature(string path)
    {
        using var stream = File.OpenRead(path);
        return stream.Length >= 2 && stream.ReadByte() == 'P' && stream.ReadByte() == 'K';
    }

    private static void TryDeleteDownloadDirectory(string downloadPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(downloadPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record BundleDownload(string Path, bool DeleteAfterImport);

    private sealed record BrandFileDownload(string Path, bool DeleteAfterImport);

    private sealed record BrandImageImportRecord(
        IReadOnlyCollection<string> PartNumberCandidates,
        string ImagesJson);

    private sealed record PartImportRow(
        string Sku,
        string SupplierPartNumber,
        string ManufacturerPartNumber,
        string? NormalizedManufacturerPartNumber,
        string Brand,
        string Description,
        string Status,
        string? Upc,
        string? Category,
        IReadOnlyDictionary<string, string?> SourceRow)
    {
        public static PartImportRow From(IReadOnlyDictionary<string, string?> row)
        {
            var sku = RequiredField(row, "Part Number");
            var supplierPartNumber = FirstField(row, "Punctuated Part Number", "Part Number") ?? sku;
            var manufacturerPartNumber = FirstField(row, "Vendor Part Number", "Vendor Punctuated Part Number") ?? sku;

            return new PartImportRow(
                sku,
                supplierPartNumber,
                manufacturerPartNumber,
                ProductMatchingService.NormalizeManufacturerPartNumber(manufacturerPartNumber),
                FirstField(row, "BRAND_NAME", "Brand Name", "Trademark") ?? "Unknown",
                FirstField(row, "Part Description", "Description") ?? sku,
                FirstField(row, "Part Status", "Status") ?? "Unknown",
                FirstField(row, "UPC_CODE", "UPC Code", "UPC"),
                FirstField(row, "PRODUCT_CODE", "Product Code", "COMMODITY_CODE", "Commodity Code", "LAST_CATALOG", "Last Catalog"),
                row);
        }
    }

    private sealed record PartMediaImage(
        string url,
        string thumbnailUrl,
        bool isPrimary,
        int sortOrder);

    private sealed class ImportCounters
    {
        public int Processed { get; set; }
        public int CreatedGlobalProducts { get; set; }
        public int UpdatedGlobalProducts { get; set; }
        public int CreatedSupplierProducts { get; set; }
        public int UpdatedSupplierProducts { get; set; }
        public int UpsertedPrices { get; set; }
    }

    private sealed class BrandImportCounters
    {
        public int BrandFilesProcessed { get; set; }
        public int BrandImageRowsProcessed { get; set; }
        public int BrandImagesUpdated { get; set; }
        public int BrandImageRowsUnmatched { get; set; }
    }

    private sealed record PartsUnlimitedBundleRunParameters(
        bool ImportBrandFiles,
        string? BrandFileUrls,
        int? BrandFileMaxFiles)
    {
        public static PartsUnlimitedBundleRunParameters FromJson(string? parametersJson)
        {
            if (string.IsNullOrWhiteSpace(parametersJson))
            {
                return new PartsUnlimitedBundleRunParameters(false, null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(parametersJson);
                var root = document.RootElement;
                return new PartsUnlimitedBundleRunParameters(
                    ReadBool(root, "ImportBrandFiles") ?? false,
                    ReadString(root, "BrandFileUrls"),
                    ReadInt(root, "BrandFileMaxFiles"));
            }
            catch (JsonException)
            {
                return new PartsUnlimitedBundleRunParameters(false, null, null);
            }
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? Clean(value.GetString())
                : null;
        }

        private static int? ReadInt(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : null;
        }

        private static bool? ReadBool(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
        }
    }

    private sealed record PartsUnlimitedConnectorOptions(
        bool ImportBrandFilesWithBundle,
        string? BrandFileUrls,
        int? BrandFileMaxFiles,
        string? DealerPortalUsername,
        string? DealerPortalPassword,
        string? DealerPortalDealerCode,
        DateTimeOffset? BrandCacheRefreshedAtUtc,
        IReadOnlyCollection<PartsUnlimitedCachedBrandRow> CachedBrands)
    {
        public static PartsUnlimitedConnectorOptions FromConfiguration(SupplierConnectorConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.ApiSecretProtected))
            {
                return Empty;
            }

            try
            {
                return Normalize(JsonSerializer.Deserialize<PartsUnlimitedConnectorOptions>(configuration.ApiSecretProtected));
            }
            catch (JsonException)
            {
                return Empty;
            }
        }

        private static PartsUnlimitedConnectorOptions Empty => new(false, null, null, null, null, null, null, []);

        private static PartsUnlimitedConnectorOptions Normalize(PartsUnlimitedConnectorOptions? options)
        {
            return options is null
                ? Empty
                : options with { CachedBrands = options.CachedBrands ?? [] };
        }
    }

    private sealed record PartsUnlimitedCachedBrandRow(
        string BrandId,
        string DisplayName,
        int Count,
        int FilteredCount);
}
