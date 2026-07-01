using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class Turn14ProductLoadsheetImportService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<Turn14ProductLoadsheetImportService> logger) : ITurn14ProductLoadsheetImportService
{
    private const int ProductLoadsheetWriteBatchSize = 1000;
    private const string Turn14SupplierCode = "TURN14";
    private const string Turn14ConnectorKey = "TURN14";
    private const string DefaultBaseUrl = "https://turn14.com";

    public async Task<Turn14ProductLoadsheetImportResult> ImportAsync(Turn14ProductLoadsheetImportRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleAsync(x => x.Id == importRun.SupplierConnectorConfigurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);

        if (!string.Equals(supplier.Code, Turn14SupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Import run {request.ImportRunId} is not a Turn14 import run.");
        }

        var credentials = Turn14ConnectorSecrets.FromConfiguration(configuration);
        if (!credentials.HasWebCredentials)
        {
            throw new InvalidOperationException("Turn14 username and password are required. Set TURN14_USERNAME and TURN14_PASSWORD or save connector credentials.");
        }

        var maxItems = request.MaxItems ?? ReadMaxItems(importRun.ParametersJson);
        var baseUrl = Clean(configuration.BaseApiUrl) ?? DefaultBaseUrl;
        var now = dateTimeProvider.UtcNow;
        var effectiveDate = DateOnly.FromDateTime(now.UtcDateTime);

        importRun.Status = "Running";
        importRun.StartedAtUtc = now;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = maxItems;
        importRun.Message = maxItems is null
            ? "Turn14 product loadsheet import started."
            : $"Turn14 product loadsheet import started. Processing first {maxItems.Value} rows.";
        await dbContext.SaveChangesAsync(cancellationToken);

        ProductLoadsheetDownload? download = null;
        try
        {
            download = await DownloadProductLoadsheetZipAsync(baseUrl, credentials, importRun.Id, cancellationToken);
            importRun.ParametersJson = MergeLoadsheetMetadata(importRun.ParametersJson, download);
            importRun.Message = BuildDownloadedLoadsheetMessage(download, maxItems);
            await dbContext.SaveChangesAsync(cancellationToken);

            importRun.ProgressTotal = maxItems;
            importRun.Message = $"{BuildDownloadedLoadsheetMessage(download, maxItems)} Importing product loadsheet rows.";
            await dbContext.SaveChangesAsync(cancellationToken);
            var counters = await ImportZipRowsAsync(download.Path, maxItems, supplier.Id, effectiveDate, now, importRun, cancellationToken);

            TrackImportRun(importRun);
            await dbContext.SaveChangesAsync(cancellationToken);

            TrackImportRun(importRun);
            importRun.Status = "Completed";
            importRun.CompletedAtUtc = dateTimeProvider.UtcNow;
            importRun.ProgressProcessed = counters.Processed;
            importRun.ProgressTotal = counters.Processed;
            importRun.Message = $"Turn14 product loadsheet import completed. Processed {counters.Processed} rows, created {counters.CreatedGlobalProducts} global products, created {counters.CreatedSupplierProducts} supplier products. {BuildLoadsheetFileSummary(download)}";
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Turn14ProductLoadsheetImportResult(
                importRun.Id,
                counters.Processed,
                counters.CreatedGlobalProducts,
                counters.UpdatedGlobalProducts,
                counters.CreatedSupplierProducts,
                counters.UpdatedSupplierProducts,
                counters.UpsertedPrices);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(download?.Path))
            {
                TryDeleteDownloadDirectory(download.Path);
            }
        }
    }

    private async Task<ProductLoadsheetDownload> DownloadProductLoadsheetZipAsync(
        string baseUrl,
        Turn14ConnectorSecrets credentials,
        Guid importRunId,
        CancellationToken cancellationToken)
    {
        var baseUri = new Uri(EnsureTrailingSlash(baseUrl));
        var cookieJar = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieJar,
            UseCookies = true,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126.0 Safari/537.36");

        using (var homeResponse = await client.GetAsync(baseUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            homeResponse.EnsureSuccessStatusCode();
        }

        using var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = credentials.WebUsername!,
            ["password"] = credentials.WebPassword!,
            ["remember"] = "1"
        });
        using var loginResponse = await client.PostAsync(new Uri(baseUri, "/user/login"), loginContent, cancellationToken);
        var authHtml = await FollowLoginResultAsync(client, baseUri, loginResponse, cancellationToken);
        if (!LooksAuthenticated(authHtml))
        {
            using var indexResponse = await client.GetAsync(new Uri(baseUri, "/index.php"), cancellationToken);
            indexResponse.EnsureSuccessStatusCode();
            authHtml = await indexResponse.Content.ReadAsStringAsync(cancellationToken);
        }

        if (!LooksAuthenticated(authHtml))
        {
            throw new InvalidOperationException("Turn14 login did not produce an authenticated session.");
        }

        using (var exportPreferencesResponse = await client.GetAsync(new Uri(baseUri, "/export_preferences.php"), cancellationToken))
        {
            exportPreferencesResponse.EnsureSuccessStatusCode();
        }

        using var exportContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["stockExport"] = "items"
        });
        using var exportResponse = await client.PostAsync(new Uri(baseUri, "/export.php"), exportContent, cancellationToken);
        exportResponse.EnsureSuccessStatusCode();

        var runDirectory = Path.Combine(Path.GetTempPath(), "im1os-turn14", importRunId.ToString("N"));
        Directory.CreateDirectory(runDirectory);
        var zipPath = Path.Combine(runDirectory, "turn14-product-loadsheet.zip");
        await using (var output = File.Create(zipPath))
        {
            await exportResponse.Content.CopyToAsync(output, cancellationToken);
        }

        if (!HasPkSignature(zipPath))
        {
            throw new InvalidOperationException("Turn14 product loadsheet response was not a ZIP file.");
        }

        return new ProductLoadsheetDownload(
            zipPath,
            exportResponse.Content.Headers.ContentDisposition?.FileNameStar ??
                exportResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'),
            exportResponse.Content.Headers.LastModified,
            dateTimeProvider.UtcNow);
    }

    private static string BuildDownloadedLoadsheetMessage(ProductLoadsheetDownload download, int? maxItems)
    {
        var limitMessage = maxItems is null
            ? "Processing all rows."
            : $"Processing first {maxItems.Value} rows.";
        return $"Turn14 product loadsheet downloaded. {BuildLoadsheetFileSummary(download)} {limitMessage}";
    }

    private static string BuildLoadsheetFileSummary(ProductLoadsheetDownload download)
    {
        var fileName = string.IsNullOrWhiteSpace(download.FileName) ? "filename unavailable" : download.FileName;
        var fileDate = download.LastModifiedUtc?.ToString("yyyy-MM-dd HH:mm") ?? "file date unavailable";
        return $"File {fileName}; file date {fileDate}; downloaded {download.DownloadedAtUtc:yyyy-MM-dd HH:mm}.";
    }

    private static string MergeLoadsheetMetadata(string? parametersJson, ProductLoadsheetDownload download)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                using var document = JsonDocument.Parse(parametersJson);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.Number when property.Value.TryGetInt32(out var number) => number,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => property.Value.GetRawText()
                    };
                }
            }
            catch (JsonException)
            {
                values["OriginalParameters"] = parametersJson;
            }
        }

        values["ProductLoadsheetFileName"] = download.FileName;
        values["ProductLoadsheetLastModifiedUtc"] = download.LastModifiedUtc;
        values["ProductLoadsheetDownloadedAtUtc"] = download.DownloadedAtUtc;
        return JsonSerializer.Serialize(values);
    }

    private async Task<ImportCounters> ImportZipRowsAsync(
        string zipPath,
        int? maxItems,
        Guid supplierId,
        DateOnly effectiveDate,
        DateTimeOffset now,
        SupplierConnectorImportRun importRun,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries
            .Where(x => x.Length > 0)
            .OrderByDescending(x => IsDelimitedEntry(x.FullName))
            .ThenBy(x => x.FullName)
            .FirstOrDefault();

        if (entry is null)
        {
            throw new InvalidOperationException("Turn14 product loadsheet ZIP did not contain a data file.");
        }

        if (!IsDelimitedEntry(entry.FullName))
        {
            throw new InvalidOperationException($"Turn14 product loadsheet '{entry.FullName}' is not a supported CSV or TXT file.");
        }

        return await ImportDelimitedRowsAsync(entry, maxItems, supplierId, effectiveDate, now, importRun, cancellationToken);
    }

    private async Task<ImportCounters> ImportDelimitedRowsAsync(
        ZipArchiveEntry entry,
        int? maxItems,
        Guid supplierId,
        DateOnly effectiveDate,
        DateTimeOffset now,
        SupplierConnectorImportRun importRun,
        CancellationToken cancellationToken)
    {
        var counters = new ImportCounters();
        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headers = await ReadCsvRecordAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Turn14 product loadsheet is missing a header row.");
        var delimiter = InferDelimiter(headers);
        if (delimiter != ',')
        {
            headers = SplitRecord(string.Join(',', headers), delimiter);
        }

        while (maxItems is null || counters.Processed < maxItems.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = new List<Turn14LoadsheetRow>(ProductLoadsheetWriteBatchSize);
            while (batch.Count < ProductLoadsheetWriteBatchSize && (maxItems is null || counters.Processed + batch.Count < maxItems.Value))
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

                batch.Add(Turn14LoadsheetRow.From(BuildRow(headers, values)));
            }

            if (batch.Count == 0)
            {
                break;
            }

            await UpsertRowBatchAsync(supplierId, batch, effectiveDate, now, counters, cancellationToken);
            await SaveLoadsheetBatchAsync(importRun, counters.Processed, cancellationToken);
            logger.LogInformation("Imported {Count} Turn14 product loadsheet rows for run {ImportRunId}.", counters.Processed, importRun.Id);
        }

        importRun.ProgressProcessed = counters.Processed;
        return counters;
    }

    private async Task UpsertRowBatchAsync(
        Guid supplierId,
        IReadOnlyCollection<Turn14LoadsheetRow> rows,
        DateOnly effectiveDate,
        DateTimeOffset now,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var skus = rows
            .Select(x => x.InternalPartNumber)
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
            UpsertRow(row);
        }

        void UpsertRow(Turn14LoadsheetRow row)
        {
            var supplierProductExists = supplierProductsBySku.TryGetValue(row.InternalPartNumber, out var supplierProduct);
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
                    Description = row.Title,
                    LongDescription = row.LongDescription,
                    Category = row.Category,
                    Upc = row.Upc,
                    ImagesJson = row.ImageJson,
                    SpecificationsJson = row.SpecificationsJson,
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
                globalProduct.Description = row.Title;
                globalProduct.LongDescription = row.LongDescription ?? globalProduct.LongDescription;
                globalProduct.Category = row.Category ?? globalProduct.Category;
                globalProduct.Upc ??= row.Upc;
                globalProduct.ImagesJson = row.ImageJson ?? globalProduct.ImagesJson;
                globalProduct.SpecificationsJson = row.SpecificationsJson;
                globalProduct.Status = IsInactiveStatus(row.Status) ? "Inactive" : "Active";
                globalProduct.IsActive = !IsInactiveStatus(row.Status);
                AddGlobalProductLookup(globalProductsByNormalizedKey, globalProductsByManufacturerKey, globalProduct);
                counters.UpdatedGlobalProducts++;
            }

            if (supplierProduct is null)
            {
                supplierProduct = new SupplierProduct
                {
                    SupplierId = supplierId,
                    GlobalProductId = globalProduct.Id,
                    SupplierSku = row.InternalPartNumber,
                    SupplierStatus = row.Status
                };
                dbContext.SupplierProducts.Add(supplierProduct);
                supplierProductsBySku[row.InternalPartNumber] = supplierProduct;
                counters.CreatedSupplierProducts++;
            }
            else
            {
                counters.UpdatedSupplierProducts++;
            }

            supplierProduct.GlobalProductId = globalProduct.Id;
            supplierProduct.SupplierDescription = row.Title;
            supplierProduct.SupplierPartNumber = row.InternalPartNumber;
            supplierProduct.ManufacturerPartNumber = row.ManufacturerPartNumber;
            supplierProduct.NormalizedManufacturerPartNumber = row.NormalizedManufacturerPartNumber;
            supplierProduct.SupplierStatus = row.Status;
            supplierProduct.SupplierImagesJson = row.ImageJson;
            supplierProduct.SourceDataJson = row.SourceDataJson;
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

            price.Msrp = row.Msrp;
            price.Map = row.Map;
            price.DealerCost = 0m;
            price.LastUpdated = now;
            counters.UpsertedPrices++;
            counters.Processed++;
        }
    }

    private async Task SaveLoadsheetBatchAsync(SupplierConnectorImportRun importRun, int processed, CancellationToken cancellationToken)
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
        Turn14LoadsheetRow row,
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

    private static async Task<string> FollowLoginResultAsync(HttpClient client, Uri baseUri, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
        {
            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(baseUri, response.Headers.Location);
            using var redirectResponse = await client.GetAsync(redirectUri, cancellationToken);
            redirectResponse.EnsureSuccessStatusCode();
            return await redirectResponse.Content.ReadAsStringAsync(cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Found or
            HttpStatusCode.SeeOther or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static bool LooksAuthenticated(string html)
    {
        return html.Contains("/user/logout", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Account #", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("LOGOUT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPkSignature(string path)
    {
        using var stream = File.OpenRead(path);
        return stream.Length >= 2 && stream.ReadByte() == 'P' && stream.ReadByte() == 'K';
    }

    private static bool IsDelimitedEntry(string fileName)
    {
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase);
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
            var header = Clean(headers[index]);
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
        return Field(row, fieldName) ?? throw new InvalidOperationException($"Turn14 product loadsheet row is missing required field '{fieldName}'.");
    }

    private static string? FirstField(IReadOnlyDictionary<string, string?> row, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var value = Field(row, fieldName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? Field(IReadOnlyDictionary<string, string?> row, string fieldName)
    {
        if (row.TryGetValue(fieldName, out var value))
        {
            return Clean(value);
        }

        var normalizedFieldName = NormalizeFieldName(fieldName);
        foreach (var field in row)
        {
            if (NormalizeFieldName(field.Key) == normalizedFieldName)
            {
                return Clean(field.Value);
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

    private static string BuildManufacturerPartNumber(string internalPartNumber, string? primaryVendor)
    {
        if (internalPartNumber.Length > 3)
        {
            return internalPartNumber[3..];
        }

        var vendor = Clean(primaryVendor);
        if (!string.IsNullOrWhiteSpace(vendor) && internalPartNumber.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
        {
            return internalPartNumber[vendor.Length..].TrimStart('-', '_', ' ');
        }

        return internalPartNumber;
    }

    private static string? JoinCategory(string? category, string? subcategory)
    {
        category = Clean(category);
        subcategory = Clean(subcategory);
        if (category is null)
        {
            return subcategory;
        }

        return subcategory is null || string.Equals(category, subcategory, StringComparison.OrdinalIgnoreCase)
            ? category
            : $"{category} / {subcategory}";
    }

    private static bool IsInactiveStatus(string status)
    {
        return status.Equals("NLA", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Inactive", StringComparison.OrdinalIgnoreCase);
    }

    private static string? MediaJson(string? primaryImage)
    {
        return string.IsNullOrWhiteSpace(primaryImage)
            ? null
            : JsonSerializer.Serialize(new[] { new { url = primaryImage, isPrimary = true } });
    }

    private static string? MediaJson(IReadOnlyCollection<Turn14Image> images, string? fallbackImage)
    {
        if (images.Count == 0)
        {
            return MediaJson(fallbackImage);
        }

        return JsonSerializer.Serialize(images.Select((image, index) => new
        {
            image.Url,
            image.Width,
            image.Height,
            image.Size,
            image.MediaContent,
            isPrimary = index == 0 || image.MediaContent?.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true
        }));
    }

    private static string SpecificationsJson(IReadOnlyDictionary<string, string?> row, string? primaryVendor, Turn14ApiItem? apiItem, Turn14ItemData? itemData)
    {
        return JsonSerializer.Serialize(new
        {
            primaryVendor,
            itemId = apiItem?.Id,
            brandId = apiItem?.BrandId,
            priceGroupId = apiItem?.PriceGroupId,
            regularStock = apiItem?.RegularStock,
            powersports = apiItem?.PowersportsIndicator,
            clearance = apiItem?.ClearanceItem,
            prop65 = apiItem?.Prop65,
            epa = apiItem?.Epa,
            unitsPerSku = apiItem?.UnitsPerSku,
            vendorName = FirstField(row, "PrimaryVendorName", "VendorName"),
            partType = FirstField(row, "PartType", "ProductType"),
            category = FirstField(row, "Category", "CategoryName"),
            subcategory = FirstField(row, "SubCategory", "SubcategoryName"),
            prop65Loadsheet = FirstField(row, "Prop65", "Prop65Warning"),
            countryOfOrigin = FirstField(row, "CountryOfOrigin", "CountryOfOriginCode"),
            descriptions = itemData?.Descriptions,
            files = itemData?.Files,
            rawColumnCount = row.Count
        });
    }

    private static int? ReadInt(JsonElement root, string objectProperty, string propertyName)
    {
        if (!root.TryGetProperty(objectProperty, out var obj) || obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
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

    private static string EnsureTrailingSlash(string value)
    {
        var clean = Clean(value) ?? DefaultBaseUrl;
        return clean.EndsWith("/", StringComparison.Ordinal) ? clean : clean + "/";
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

    private static string NormalizeFieldName(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private sealed record Turn14LoadsheetRow(
        string InternalPartNumber,
        string? PrimaryVendor,
        string ManufacturerPartNumber,
        string? NormalizedManufacturerPartNumber,
        string Brand,
        string? Upc,
        string Title,
        string? LongDescription,
        string? Category,
        string Status,
        string? ImageJson,
        string SpecificationsJson,
        string SourceDataJson,
        decimal? Msrp,
        decimal? Map)
    {
        public static Turn14LoadsheetRow From(IReadOnlyDictionary<string, string?> row)
        {
            var internalPartNumber = RequiredField(row, "InternalPartNumber");
            var primaryVendor = Field(row, "PrimaryVendor");
            var manufacturerPartNumber = BuildManufacturerPartNumber(internalPartNumber, primaryVendor);
            var normalizedManufacturerPartNumber = ProductMatchingService.NormalizeManufacturerPartNumber(manufacturerPartNumber);
            var brand = FirstField(row, "Brand", "BrandName", "Manufacturer", "ManufacturerName", "ProductLine") ?? primaryVendor ?? "Unknown";
            var title = FirstField(row, "Title", "ItemTitle", "ItemDescription", "ProductName", "Description", "ProductDescription", "PartDescription", "Name") ?? internalPartNumber;
            var status = FirstField(row, "Status", "PartStatus", "LifecycleStatus") ?? "Active";

            return new Turn14LoadsheetRow(
                internalPartNumber,
                primaryVendor,
                manufacturerPartNumber,
                normalizedManufacturerPartNumber,
                brand,
                FirstField(row, "UPC", "UPCCode", "Barcode", "GTIN"),
                title,
                FirstField(row, "LongDescription", "ExtendedDescription", "ProductLongDescription", "Description", "MarketingDescription"),
                FirstField(row, "Category", "CategoryName", "ProductCategory", "PartType", "ProductType", "Department"),
                status,
                MediaJson(Array.Empty<Turn14Image>(), FirstField(row, "ImageURL", "ImageUrl", "Image", "PrimaryImage", "PrimaryImageURL", "PhotoURL", "Thumbnail")),
                Turn14ProductLoadsheetImportService.SpecificationsJson(row, primaryVendor, null, null),
                JsonSerializer.Serialize(new { loadsheet = row, apiItem = (Turn14ApiItem?)null, itemData = (Turn14ItemData?)null }),
                DecimalField(row, "MSRP", "RetailPrice", "ListPrice", "Retail"),
                DecimalField(row, "MAP", "MapPrice", "MinimumAdvertisedPrice"));
        }
    }

    private sealed record ProductLoadsheetDownload(
        string Path,
        string? FileName,
        DateTimeOffset? LastModifiedUtc,
        DateTimeOffset DownloadedAtUtc);

    private sealed record Turn14ApiItem(
        string Id,
        string? PartNumber,
        string? ProductName,
        string? PartDescription,
        string? Category,
        string? Subcategory,
        string? Brand,
        int? BrandId,
        int? PriceGroupId,
        bool? Active,
        bool? RegularStock,
        bool? PowersportsIndicator,
        bool? ClearanceItem,
        string? Prop65,
        string? Epa,
        int? UnitsPerSku,
        string? Thumbnail,
        string? Barcode,
        decimal? Length,
        decimal? Width,
        decimal? Height,
        decimal? Weight,
        string? WarehouseAvailabilityJson)
    {
        public static Turn14ApiItem FromJson(JsonElement item)
        {
            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;
            var dimensions = attributes.TryGetProperty("dimensions", out var dimensionsElement) && dimensionsElement.ValueKind == JsonValueKind.Array
                ? dimensionsElement.EnumerateArray().FirstOrDefault()
                : default;

            return new Turn14ApiItem(
                StringValue(item, "id") ?? string.Empty,
                StringValue(attributes, "part_number"),
                StringValue(attributes, "product_name"),
                StringValue(attributes, "part_description"),
                StringValue(attributes, "category"),
                StringValue(attributes, "subcategory"),
                StringValue(attributes, "brand"),
                IntValue(attributes, "brand_id"),
                IntValue(attributes, "price_group_id"),
                BoolValue(attributes, "active"),
                BoolValue(attributes, "regular_stock"),
                BoolValue(attributes, "powersports_indicator"),
                BoolValue(attributes, "clearance_item"),
                StringValue(attributes, "prop_65"),
                StringValue(attributes, "epa"),
                IntValue(attributes, "units_per_sku"),
                StringValue(attributes, "thumbnail"),
                StringValue(attributes, "barcode"),
                dimensions.ValueKind == JsonValueKind.Object ? DecimalValue(dimensions, "length") : null,
                dimensions.ValueKind == JsonValueKind.Object ? DecimalValue(dimensions, "width") : null,
                dimensions.ValueKind == JsonValueKind.Object ? DecimalValue(dimensions, "height") : null,
                dimensions.ValueKind == JsonValueKind.Object ? DecimalValue(dimensions, "weight") : null,
                attributes.TryGetProperty("warehouse_availability", out var warehouseAvailability)
                    ? warehouseAvailability.GetRawText()
                    : null);
        }
    }

    private sealed record Turn14ItemData(
        IReadOnlyCollection<Turn14Image> Images,
        IReadOnlyCollection<Turn14Description> Descriptions,
        IReadOnlyCollection<Turn14File> Files)
    {
        public string? BestDescription => Descriptions
            .OrderByDescending(x => string.Equals(x.Type, "Market Description", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Description?.Length ?? 0)
            .Select(x => x.Description)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        public static Turn14ItemData FromJson(JsonElement item)
        {
            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;
            var images = new List<Turn14Image>();
            var files = new List<Turn14File>();
            if (attributes.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in filesElement.EnumerateArray())
                {
                    var fileType = StringValue(file, "type");
                    var mediaContent = StringValue(file, "media_content");
                    var extension = StringValue(file, "file_extension");
                    var fileRecord = new Turn14File(fileType, extension, mediaContent);
                    files.Add(fileRecord);
                    if (!string.Equals(fileType, "Image", StringComparison.OrdinalIgnoreCase) ||
                        !file.TryGetProperty("links", out var links) ||
                        links.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var bestLink = links
                        .EnumerateArray()
                        .OrderByDescending(x => StringValue(x, "size") == "L")
                        .ThenByDescending(x => DecimalValue(x, "width") ?? 0)
                        .FirstOrDefault();
                    var url = StringValue(bestLink, "url");
                    if (url is null)
                    {
                        continue;
                    }

                    images.Add(new Turn14Image(
                        url,
                        DecimalValue(bestLink, "width"),
                        DecimalValue(bestLink, "height"),
                        StringValue(bestLink, "size"),
                        mediaContent));
                }
            }

            var descriptions = new List<Turn14Description>();
            if (attributes.TryGetProperty("descriptions", out var descriptionsElement) && descriptionsElement.ValueKind == JsonValueKind.Array)
            {
                descriptions.AddRange(descriptionsElement
                    .EnumerateArray()
                    .Select(x => new Turn14Description(StringValue(x, "type"), StringValue(x, "description")))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Description)));
            }

            return new Turn14ItemData(images, descriptions, files);
        }
    }

    private sealed record Turn14Image(string Url, decimal? Width, decimal? Height, string? Size, string? MediaContent);

    private sealed record Turn14Description(string? Type, string? Description);

    private sealed record Turn14File(string? Type, string? FileExtension, string? MediaContent);

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

    private static int? IntValue(JsonElement item, string propertyName)
    {
        var value = StringValue(item, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
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

        return value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static bool? BoolValue(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
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

    private sealed class Turn14RateLimitException(string message) : Exception(message);
}
