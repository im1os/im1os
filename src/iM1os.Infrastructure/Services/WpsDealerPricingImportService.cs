using System.Globalization;
using System.Text;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class WpsDealerPricingImportService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<WpsDealerPricingImportService> logger) : IWpsDealerPricingImportService
{
    private const string WpsSupplierCode = "WPS";
    private const string DealerPricingPath = "/dealer-pricing";
    private const int DealerPricingFileMaxPollAttempts = 60;
    private static readonly TimeSpan DealerPricingFilePollDelay = TimeSpan.FromSeconds(10);

    public async Task<WpsDealerPricingImportResult> ImportAsync(WpsDealerPricingImportRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.CompanySupplierConnectorImportRuns
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .SingleAsync(x =>
                x.OrganizationId == importRun.OrganizationId &&
                x.Id == importRun.CompanySupplierConnectorConfigurationId,
                cancellationToken);
        var supplier = await dbContext.Suppliers
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);

        if (!string.Equals(supplier.Code, WpsSupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only WPS dealer pricing imports are currently supported.");
        }

        var apiKey = Clean(configuration.ApiKey)
            ?? throw new InvalidOperationException("WPS API key is required for dealer pricing sync.");
        var maxItems = ReadMaxItems(importRun.ParametersJson);
        var now = dateTimeProvider.UtcNow;
        var pricingFile = await DownloadPricingRowsAsync(configuration.BaseApiUrl, apiKey, cancellationToken);
        var pricingRows = pricingFile.Rows;
        importRun.Source = pricingFile.FileUrl;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = maxItems ?? pricingRows.Count;
        importRun.ParametersJson = JsonSerializer.Serialize(new
        {
            MaxItems = maxItems,
            PriceFileUrl = pricingFile.FileUrl,
            PriceFileLastModifiedUtc = pricingFile.LastModifiedUtc,
            PriceFileDownloadedAtUtc = now
        });
        var counters = new Counters();

        foreach (var row in pricingRows)
        {
            counters.RowsProcessed++;
            var supplierSku = Clean(row.SupplierSku);
            var sourceSupplierProductId = Clean(row.SourceSupplierProductId);
            if ((supplierSku is null && sourceSupplierProductId is null) || row.ActualDealerCost is null)
            {
                counters.UnmatchedRows++;
                continue;
            }

            var supplierProduct = await dbContext.SupplierProducts
                .OrderBy(x => x.SupplierSku)
                .FirstOrDefaultAsync(x =>
                    x.SupplierId == supplier.Id &&
                    ((supplierSku != null && x.SupplierSku == supplierSku) ||
                     (sourceSupplierProductId != null && x.SourceSupplierProductId == sourceSupplierProductId)),
                    cancellationToken);
            if (supplierProduct is null)
            {
                counters.UnmatchedRows++;
                continue;
            }

            var price = await dbContext.CompanySupplierPrices
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.OrganizationId == importRun.OrganizationId && x.SupplierProductId == supplierProduct.Id, cancellationToken);
            if (price is null)
            {
                price = new CompanySupplierPrice
                {
                    OrganizationId = importRun.OrganizationId,
                    SupplierId = supplier.Id,
                    SupplierProductId = supplierProduct.Id,
                    SupplierSku = supplierProduct.SupplierSku,
                    SourceSupplierProductId = supplierProduct.SourceSupplierProductId,
                    ActualDealerCost = row.ActualDealerCost.Value,
                    LastSyncedAtUtc = now
                };
                dbContext.CompanySupplierPrices.Add(price);
            }

            price.SupplierSku = supplierProduct.SupplierSku;
            price.SourceSupplierProductId = supplierProduct.SourceSupplierProductId;
            price.ActualDealerCost = row.ActualDealerCost.Value;
            price.Currency = Clean(row.Currency) ?? "USD";
            price.EffectiveDate = row.EffectiveDate;
            price.LastSyncedAtUtc = now;
            price.SourceDataJson = row.SourceJson;
            counters.PricesUpserted++;
            if (maxItems is not null && counters.PricesUpserted >= maxItems.Value)
            {
                importRun.ProgressProcessed = counters.RowsProcessed;
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }

            if (counters.RowsProcessed % 100 == 0)
            {
                importRun.ProgressProcessed = counters.RowsProcessed;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        logger.LogInformation(
            "Completed WPS dealer pricing import run {ImportRunId}. RowsProcessed={RowsProcessed}, PricesUpserted={PricesUpserted}, UnmatchedRows={UnmatchedRows}.",
            importRun.Id,
            counters.RowsProcessed,
            counters.PricesUpserted,
            counters.UnmatchedRows);
        importRun.ProgressProcessed = counters.RowsProcessed;
        importRun.ProgressTotal ??= counters.RowsProcessed;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new WpsDealerPricingImportResult(counters.RowsProcessed, counters.PricesUpserted, counters.UnmatchedRows, pricingFile.FileUrl, pricingFile.LastModifiedUtc, now);
    }

    private async Task<PricingFileDownload> DownloadPricingRowsAsync(string? baseApiUrl, string apiKey, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("WpsDataDepot");
        var fileLocation = await RequestDealerPricingFileLocationAsync(client, baseApiUrl, apiKey, cancellationToken);
        if (fileLocation.DirectBody is not null)
        {
            var directRows = LooksLikeJson(fileLocation.ContentType ?? string.Empty, fileLocation.DirectBody)
                ? ParseJsonPricingRows(fileLocation.DirectBody)
                : ParseCsvPricingRows(fileLocation.DirectBody);
            return new PricingFileDownload(fileLocation.FileUrl, fileLocation.LastModifiedUtc, directRows);
        }

        using var response = await client.GetAsync(fileLocation.FileUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(contentType) && fileLocation.FileUrl.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "text/csv";
        }

        var rows = LooksLikeJson(contentType, body)
            ? ParseJsonPricingRows(body)
            : ParseCsvPricingRows(body);
        return new PricingFileDownload(fileLocation.FileUrl, response.Content.Headers.LastModified, rows);
    }

    private static async Task<PricingFileLocation> RequestDealerPricingFileLocationAsync(HttpClient client, string? baseApiUrl, string apiKey, CancellationToken cancellationToken)
    {
        var requestUri = BuildDealerPricingUri(baseApiUrl);
        for (var attempt = 1; attempt <= DealerPricingFileMaxPollAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new("Bearer", apiKey);
            using var response = await client.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode == 202)
            {
                if (attempt == DealerPricingFileMaxPollAttempts)
                {
                    break;
                }

                if (attempt > 1)
                {
                    await Task.Delay(PollDelay(response), cancellationToken);
                }

                continue;
            }

            response.EnsureSuccessStatusCode();
            var location = response.Headers.Location ?? response.Content.Headers.ContentLocation;
            if (location is not null)
            {
                return new PricingFileLocation(location.IsAbsoluteUri ? location.ToString() : new Uri(requestUri, location).ToString(), null, null, null);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var downloadUrl = TryReadDownloadUrl(body);
            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                return new PricingFileLocation(downloadUrl, null, null, null);
            }

            if (LooksLikePricingFile(contentType, body))
            {
                return new PricingFileLocation(requestUri.ToString(), body, contentType, response.Content.Headers.LastModified);
            }

            if (attempt == DealerPricingFileMaxPollAttempts)
            {
                break;
            }

            if (attempt > 1)
            {
                await Task.Delay(PollDelay(response), cancellationToken);
            }
        }

        throw new InvalidOperationException("WPS dealer pricing file was not available after the polling window. The next scheduled sync will retry.");
    }

    private static TimeSpan PollDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero && delta < TimeSpan.FromMinutes(5))
        {
            return delta;
        }

        return DealerPricingFilePollDelay;
    }

    private static Uri BuildDealerPricingUri(string? baseApiUrl)
    {
        var cleanBaseUrl = Clean(baseApiUrl) ?? "https://api.wps-inc.com";
        if (cleanBaseUrl.EndsWith(DealerPricingPath, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(cleanBaseUrl);
        }

        return new Uri($"{cleanBaseUrl.TrimEnd('/')}{DealerPricingPath}");
    }

    private static IReadOnlyCollection<PricingRow> ParseJsonPricingRows(string body)
    {
        using var document = JsonDocument.Parse(body);
        var rows = new List<PricingRow>();
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (TryFindArray(root, out var array))
            {
                root = array;
            }
            else
            {
                rows.Add(ReadJsonPricingRow(root));
                return rows;
            }
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                rows.Add(ReadJsonPricingRow(item));
            }
        }

        return rows;
    }

    private static PricingRow ReadJsonPricingRow(JsonElement item)
    {
        var sourceSupplierProductId = ReadString(item, "id", "itemId", "item_id", "sourceSupplierProductId", "source_supplier_product_id");
        var sku = ReadString(item, "sku", "supplierSku", "itemSku", "item_sku", "partNumber", "part_number", "itemNumber", "item_number");
        var cost = ReadDecimal(item, "actualDealerCost", "actual_dealer_cost", "actualDealerPrice", "actual_dealer_price", "dealerCost", "dealer_cost", "dealerPrice", "dealer_price", "standardDealerPrice", "standard_dealer_price", "cost", "price");
        var currency = ReadString(item, "currency", "currencyCode", "currency_code");
        var effectiveDate = ReadDate(item, "effectiveDate", "effective_date");
        return new PricingRow(sourceSupplierProductId, sku, cost, currency, effectiveDate, item.GetRawText());
    }

    private static IReadOnlyCollection<PricingRow> ParseCsvPricingRows(string body)
    {
        var records = ReadCsvRecords(body).ToList();
        if (records.Count < 2)
        {
            return [];
        }

        var headers = records[0].Select(NormalizeHeader).ToList();
        var sourceSupplierProductIdIndex = FindHeader(headers, "id", "itemid", "sourceSupplierProductId");
        var skuIndex = FindHeader(headers, "sku", "suppliersku", "itemsku", "itemnumber", "partnumber");
        var costIndex = FindHeader(headers, "actualdealercost", "actualdealerprice", "dealercost", "dealerprice", "standarddealerprice", "cost", "price");
        var currencyIndex = FindHeader(headers, "currency", "currencycode");
        var effectiveDateIndex = FindHeader(headers, "effectivedate");
        if (skuIndex < 0 || costIndex < 0)
        {
            return [];
        }

        var rows = new List<PricingRow>();
        foreach (var record in records.Skip(1))
        {
            var sourceSupplierProductId = sourceSupplierProductIdIndex < 0 ? null : ReadCsvValue(record, sourceSupplierProductIdIndex);
            var sku = ReadCsvValue(record, skuIndex);
            var cost = TryParseDecimal(ReadCsvValue(record, costIndex));
            var currency = currencyIndex < 0 ? null : ReadCsvValue(record, currencyIndex);
            var effectiveDate = effectiveDateIndex < 0 ? null : TryParseDate(ReadCsvValue(record, effectiveDateIndex));
            rows.Add(new PricingRow(sourceSupplierProductId, sku, cost, currency, effectiveDate, CsvSourceJson(headers, record)));
        }

        return rows;
    }

    private static IEnumerable<IReadOnlyList<string>> ReadCsvRecords(string body)
    {
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < body.Length; i++)
        {
            var current = body[i];
            if (current == '"')
            {
                if (inQuotes && i + 1 < body.Length && body[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (current == ',' && !inQuotes)
            {
                row.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if ((current == '\r' || current == '\n') && !inQuotes)
            {
                if (current == '\r' && i + 1 < body.Length && body[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    yield return row;
                }

                row = [];
                continue;
            }

            cell.Append(current);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return row;
            }
        }
    }

    private static string CsvSourceJson(IReadOnlyList<string> headers, IReadOnlyList<string> record)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < headers.Count; i++)
        {
            values[headers[i]] = ReadCsvValue(record, i);
        }

        return JsonSerializer.Serialize(values);
    }

    private static string? TryReadDownloadUrl(string body)
    {
        if (Uri.TryCreate(Clean(body), UriKind.Absolute, out var bodyUri))
        {
            return bodyUri.ToString();
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return ReadString(
                document.RootElement,
                "url",
                "downloadUrl",
                "download_url",
                "fileUrl",
                "file_url",
                "priceFileUrl",
                "price_file_url",
                "dealerPricingUrl",
                "dealer_pricing_url",
                "href",
                "location",
                "file",
                "download") ??
                FindFirstAbsoluteUrl(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindFirstAbsoluteUrl(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(Clean(element.GetString()), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var value = FindFirstAbsoluteUrl(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = FindFirstAbsoluteUrl(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryFindArray(JsonElement root, out JsonElement array)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                array = property.Value;
                return true;
            }
        }

        array = default;
        return false;
    }

    private static bool LooksLikeJson(string contentType, string body)
    {
        return contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            body.TrimStart().StartsWith('{') ||
            body.TrimStart().StartsWith('[');
    }

    private static bool LooksLikePricingFile(string contentType, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            var rows = LooksLikeJson(contentType, body)
                ? ParseJsonPricingRows(body)
                : ParseCsvPricingRows(body);
            return rows.Any(x => !string.IsNullOrWhiteSpace(x.SupplierSku) && x.ActualDealerCost is not null);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return property.ValueKind == JsonValueKind.String
                ? Clean(property.GetString())
                : Clean(property.ToString());
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && TryParseDecimal(property.GetString()) is decimal stringValue)
            {
                return stringValue;
            }
        }

        return null;
    }

    private static DateOnly? ReadDate(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var property))
            {
                continue;
            }

            var parsed = property.ValueKind == JsonValueKind.String
                ? TryParseDate(property.GetString())
                : TryParseDate(property.ToString());
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement item, string name, out JsonElement property)
    {
        if (item.TryGetProperty(name, out property))
        {
            return true;
        }

        foreach (var itemProperty in item.EnumerateObject())
        {
            if (string.Equals(NormalizeHeader(itemProperty.Name), NormalizeHeader(name), StringComparison.OrdinalIgnoreCase))
            {
                property = itemProperty.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static decimal? TryParseDecimal(string? value)
    {
        var clean = Clean(value)?.Replace("$", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateOnly? TryParseDate(string? value)
    {
        return DateOnly.TryParse(Clean(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private static int FindHeader(IReadOnlyList<string> headers, params string[] names)
    {
        foreach (var name in names.Select(NormalizeHeader))
        {
            for (var index = 0; index < headers.Count; index++)
            {
                if (headers[index] == name)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static string? ReadCsvValue(IReadOnlyList<string> record, int index)
    {
        return index >= 0 && index < record.Count ? Clean(record[index]) : null;
    }

    private static int? ReadMaxItems(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(parametersJson);
        return document.RootElement.TryGetProperty("MaxItems", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var maxItems) && maxItems > 0
            ? maxItems
            : null;
    }

    private static string NormalizeHeader(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record PricingRow(string? SourceSupplierProductId, string? SupplierSku, decimal? ActualDealerCost, string? Currency, DateOnly? EffectiveDate, string SourceJson);

    private sealed record PricingFileLocation(string FileUrl, string? DirectBody, string? ContentType, DateTimeOffset? LastModifiedUtc);

    private sealed record PricingFileDownload(string FileUrl, DateTimeOffset? LastModifiedUtc, IReadOnlyCollection<PricingRow> Rows);

    private sealed class Counters
    {
        public int RowsProcessed { get; set; }

        public int PricesUpserted { get; set; }

        public int UnmatchedRows { get; set; }
    }
}
