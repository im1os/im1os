using System.Globalization;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class PartsUnlimitedDealerPricingImportService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<PartsUnlimitedDealerPricingImportService> logger) : IPartsUnlimitedDealerPricingImportService
{
    private const int MaxBatchSize = 500;
    private const string SupplierCode = "PU";
    private const string DefaultBaseApiUrl = "https://api.parts-unlimited.com/api";

    public async Task<SupplierDealerPricingImportResult> ImportAsync(PartsUnlimitedDealerPricingImportRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.CompanySupplierConnectorImportRuns
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == importRun.OrganizationId && x.Id == importRun.CompanySupplierConnectorConfigurationId, cancellationToken);
        var supplier = await dbContext.Suppliers.SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);
        if (!string.Equals(supplier.Code, SupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Parts Unlimited dealer pricing imports are supported by this service.");
        }

        var apiKey = Clean(configuration.ApiKey)
            ?? throw new InvalidOperationException("Parts Unlimited API key is required for dealer pricing sync.");
        var maxItems = ReadMaxItems(importRun.ParametersJson);
        var now = dateTimeProvider.UtcNow;
        var baseApiUrl = (Clean(configuration.BaseApiUrl) ?? DefaultBaseApiUrl).TrimEnd('/');
        var source = $"{baseApiUrl}/v1/parts/pricing";
        var supplierProductsQuery = dbContext.SupplierProducts
            .Where(x => x.SupplierId == supplier.Id)
            .OrderBy(x => x.SupplierSku);
        var supplierProducts = maxItems is null
            ? await supplierProductsQuery.ToListAsync(cancellationToken)
            : await supplierProductsQuery.Take(maxItems.Value).ToListAsync(cancellationToken);
        var productsByPartNumber = supplierProducts
            .Select(x => new
            {
                Product = x,
                PartNumber = Clean(x.SupplierSku) ?? Clean(x.SupplierPartNumber)
            })
            .Where(x => x.PartNumber is not null)
            .GroupBy(x => x.PartNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(item => item.Product).ToList(), StringComparer.OrdinalIgnoreCase);

        importRun.Source = source;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = supplierProducts.Count;
        importRun.ParametersJson = JsonSerializer.Serialize(new
        {
            MaxItems = maxItems,
            PriceFileUrl = source,
            PriceFileDownloadedAtUtc = now
        });

        var counters = new Counters();
        var productsWithPartNumbers = productsByPartNumber.Values.Sum(x => x.Count);
        counters.RowsProcessed = supplierProducts.Count - productsWithPartNumbers;
        counters.UnmatchedRows = counters.RowsProcessed;
        foreach (var partNumberChunk in productsByPartNumber.Keys.Chunk(MaxBatchSize))
        {
            var rows = await DownloadPricingRowsAsync(baseApiUrl, apiKey, partNumberChunk, cancellationToken);
            var rowsByPartNumber = rows
                .Where(x => x.SupplierSku is not null)
                .GroupBy(x => x.SupplierSku!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var partNumber in partNumberChunk)
            {
                if (!productsByPartNumber.TryGetValue(partNumber, out var matchingProducts))
                {
                    continue;
                }

                counters.RowsProcessed += matchingProducts.Count;
                if (!rowsByPartNumber.TryGetValue(partNumber, out var row) || row.ActualDealerCost is null)
                {
                    counters.UnmatchedRows += matchingProducts.Count;
                    continue;
                }

                foreach (var supplierProduct in matchingProducts)
                {
                    await UpsertCompanyPriceAsync(importRun.OrganizationId, supplier.Id, supplierProduct, row, now, cancellationToken);
                    counters.PricesUpserted++;
                }
            }

            importRun.ProgressProcessed = counters.RowsProcessed;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        importRun.ProgressProcessed = counters.RowsProcessed;
        importRun.ProgressTotal ??= counters.RowsProcessed;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Completed Parts Unlimited dealer pricing import run {ImportRunId}. RowsProcessed={RowsProcessed}, PricesUpserted={PricesUpserted}, UnmatchedRows={UnmatchedRows}.",
            importRun.Id,
            counters.RowsProcessed,
            counters.PricesUpserted,
            counters.UnmatchedRows);
        return new SupplierDealerPricingImportResult(counters.RowsProcessed, counters.PricesUpserted, counters.UnmatchedRows, source, null, now);
    }

    private async Task<IReadOnlyCollection<PricingRow>> DownloadPricingRowsAsync(string baseApiUrl, string apiKey, IReadOnlyCollection<string> partNumbers, CancellationToken cancellationToken)
    {
        var escapedPartNumbers = partNumbers.Select(Uri.EscapeDataString);
        var requestUri = $"{baseApiUrl.TrimEnd('/')}/v1/parts/pricing/{string.Join(",", escapedPartNumbers)}";
        var client = httpClientFactory.CreateClient("PartsUnlimitedApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Parts Unlimited pricing request failed with HTTP {(int)response.StatusCode}: {TrimForMessage(body)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParsePricingRows(document.RootElement);
    }

    private async Task UpsertCompanyPriceAsync(Guid organizationId, Guid supplierId, SupplierProduct supplierProduct, PricingRow row, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var price = await dbContext.CompanySupplierPrices
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.SupplierProductId == supplierProduct.Id, cancellationToken);
        if (price is null)
        {
            price = new CompanySupplierPrice
            {
                OrganizationId = organizationId,
                SupplierId = supplierId,
                SupplierProductId = supplierProduct.Id,
                SupplierSku = supplierProduct.SupplierSku,
                SourceSupplierProductId = supplierProduct.SourceSupplierProductId,
                ActualDealerCost = row.ActualDealerCost!.Value,
                LastSyncedAtUtc = now
            };
            dbContext.CompanySupplierPrices.Add(price);
        }

        price.SupplierSku = supplierProduct.SupplierSku;
        price.SourceSupplierProductId = supplierProduct.SourceSupplierProductId;
        price.ActualDealerCost = row.ActualDealerCost!.Value;
        price.Currency = Clean(row.Currency) ?? "USD";
        price.EffectiveDate = row.EffectiveDate;
        price.LastSyncedAtUtc = now;
        price.SourceDataJson = row.SourceJson;
    }

    private static IReadOnlyCollection<PricingRow> ParsePricingRows(JsonElement root)
    {
        var data = SelectData(root);
        var rows = new List<PricingRow>();
        if (data.ValueKind == JsonValueKind.Object && LooksLikePricingItem(data))
        {
            rows.Add(ReadPricingRow(data));
            return rows;
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in data.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    rows.Add(ReadPricingRow(property.Value, property.Name));
                }
            }
        }

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    rows.Add(ReadPricingRow(item));
                }
            }
        }

        return rows;
    }

    private static JsonElement SelectData(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "pricing", "prices", "parts" })
            {
                if (data.TryGetProperty(propertyName, out var nested) && nested.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    return nested;
                }
            }
        }

        return data;
    }

    private static bool LooksLikePricingItem(JsonElement item)
    {
        var attributes = item.TryGetProperty("attributes", out var attr) && attr.ValueKind == JsonValueKind.Object ? attr : item;
        return ReadString(attributes, "partNumber", "part_number", "partnumber", "sku", "id") is not null ||
            ReadDecimal(attributes, "dealerCost", "dealer_cost", "dealerPrice", "dealer_price", "actualDealerCost", "actual_dealer_cost", "cost", "price") is not null;
    }

    private static PricingRow ReadPricingRow(JsonElement item, string? fallbackSku = null)
    {
        var attributes = item.TryGetProperty("attributes", out var attr) && attr.ValueKind == JsonValueKind.Object ? attr : item;
        var sourceId = ReadString(item, "id", "partId", "part_id") ?? ReadString(attributes, "id", "partId", "part_id");
        var sku = ReadString(attributes, "partNumber", "part_number", "partnumber", "sku", "part", "itemNumber", "item_number") ?? fallbackSku;
        var cost = ReadDecimal(attributes, "actualDealerCost", "actual_dealer_cost", "dealerCost", "dealer_cost", "dealerPrice", "dealer_price", "currentDealerPrice", "current_dealer_price", "netPrice", "net_price", "cost", "price");
        var currency = ReadString(attributes, "currency", "currencyCode", "currency_code");
        var effectiveDate = ReadDate(attributes, "effectiveDate", "effective_date");
        return new PricingRow(sourceId, Clean(sku), cost, currency, effectiveDate, item.GetRawText());
    }

    private static string? ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return property.ValueKind == JsonValueKind.String ? Clean(property.GetString()) : Clean(property.ToString());
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

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String && TryParseDecimal(property.GetString()) is decimal value)
            {
                return value;
            }
        }

        return null;
    }

    private static DateOnly? ReadDate(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(item, name, out var property) && DateOnly.TryParse(property.ToString(), out var parsed))
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

        var normalized = Normalize(name);
        foreach (var itemProperty in item.EnumerateObject())
        {
            if (Normalize(itemProperty.Name) == normalized)
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

    private static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string TrimForMessage(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return "no response body";
        }

        clean = clean.ReplaceLineEndings(" ");
        return clean.Length <= 500 ? clean : $"{clean[..500]}...";
    }

    private sealed record PricingRow(string? SourceSupplierProductId, string? SupplierSku, decimal? ActualDealerCost, string? Currency, DateOnly? EffectiveDate, string SourceJson);

    private sealed class Counters
    {
        public int RowsProcessed { get; set; }

        public int PricesUpserted { get; set; }

        public int UnmatchedRows { get; set; }
    }
}
