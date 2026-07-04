using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class Turn14DealerPricingImportService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<Turn14DealerPricingImportService> logger) : ITurn14DealerPricingImportService
{
    private const string SupplierCode = "TURN14";
    private const string DefaultBaseApiUrl = "https://api.turn14.com";

    public async Task<SupplierDealerPricingImportResult> ImportAsync(Turn14DealerPricingImportRequest request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Turn14 dealer pricing imports are supported by this service.");
        }

        var clientId = Clean(configuration.ApiKey)
            ?? throw new InvalidOperationException("Turn14 client id is required for dealer pricing sync.");
        var clientSecret = Clean(configuration.ApiSecretProtected)
            ?? throw new InvalidOperationException("Turn14 client secret is required for dealer pricing sync.");
        var maxItems = ReadMaxItems(importRun.ParametersJson);
        var now = dateTimeProvider.UtcNow;
        var baseApiUrl = NormalizeBaseApiUrl(Clean(configuration.BaseApiUrl) ?? DefaultBaseApiUrl);
        var source = $"{baseApiUrl}/v1/pricing";
        var tokenProvider = new Turn14AccessTokenProvider(tokenCancellationToken =>
            RequestAccessTokenAsync(baseApiUrl, clientId, clientSecret, tokenCancellationToken));
        var supplierProducts = await dbContext.SupplierProducts
            .AsNoTracking()
            .Where(x => x.SupplierId == supplier.Id)
            .OrderBy(x => x.SupplierSku)
            .ToListAsync(cancellationToken);
        var productsBySourceId = BuildProductLookup(supplierProducts, x => x.SourceSupplierProductId);
        var productsBySku = BuildProductLookup(supplierProducts, x => x.SupplierSku);
        var productsBySupplierPartNumber = BuildProductLookup(supplierProducts, x => x.SupplierPartNumber);

        importRun.Source = source;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = maxItems;
        importRun.ParametersJson = JsonSerializer.Serialize(new
        {
            MaxItems = maxItems,
            PriceFileUrl = source,
            PriceFileDownloadedAtUtc = now
        });

        var counters = new Counters();
        var nextRequestUri = source;
        while (nextRequestUri is not null && (maxItems is null || counters.RowsProcessed < maxItems.Value))
        {
            var page = await DownloadPricingPageAsync(baseApiUrl, nextRequestUri, tokenProvider, cancellationToken);
            if (importRun.ProgressTotal is null && page.TotalPages is > 0)
            {
                importRun.ProgressTotal = page.TotalPages.Value * Math.Max(page.Rows.Count, 1);
            }

            var priceUpdates = new List<PriceUpdate>();
            foreach (var row in page.Rows)
            {
                if (maxItems is not null && counters.RowsProcessed >= maxItems.Value)
                {
                    break;
                }

                counters.RowsProcessed++;
                if (row.ActualDealerCost is null)
                {
                    counters.UnmatchedRows++;
                    continue;
                }

                var supplierProduct = FindSupplierProduct(row, productsBySourceId, productsBySku, productsBySupplierPartNumber);
                if (supplierProduct is null)
                {
                    counters.UnmatchedRows++;
                    continue;
                }

                priceUpdates.Add(new PriceUpdate(supplierProduct, row));
                counters.PricesUpserted++;
            }

            await UpsertCompanyPricesAsync(importRun.OrganizationId, supplier.Id, priceUpdates, now, cancellationToken);
            nextRequestUri = page.NextRequestUri;
            importRun.ProgressProcessed = counters.RowsProcessed;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        importRun.ProgressProcessed = counters.RowsProcessed;
        importRun.ProgressTotal ??= counters.RowsProcessed;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Completed Turn14 dealer pricing import run {ImportRunId}. RowsProcessed={RowsProcessed}, PricesUpserted={PricesUpserted}, UnmatchedRows={UnmatchedRows}.",
            importRun.Id,
            counters.RowsProcessed,
            counters.PricesUpserted,
            counters.UnmatchedRows);
        return new SupplierDealerPricingImportResult(counters.RowsProcessed, counters.PricesUpserted, counters.UnmatchedRows, source, null, now);
    }

    private async Task<string> RequestAccessTokenAsync(string baseApiUrl, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Turn14Api");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });
        using var response = await client.PostAsync(ResolveRequestUri(baseApiUrl, "/v1/token"), content, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("access_token", out var tokenElement) ||
            tokenElement.ValueKind != JsonValueKind.String ||
            Clean(tokenElement.GetString()) is not { } token)
        {
            throw new InvalidOperationException("Turn14 token response did not include an access token.");
        }

        return token;
    }

    private async Task<PricingPage> DownloadPricingPageAsync(
        string baseApiUrl,
        string requestUri,
        Turn14AccessTokenProvider tokenProvider,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Turn14Api");
        var refreshedForUnauthorized = false;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ResolveRequestUri(baseApiUrl, requestUri));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetAsync(cancellationToken));
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedForUnauthorized)
            {
                await tokenProvider.RefreshAsync(cancellationToken);
                refreshedForUnauthorized = true;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Turn14 pricing request failed with HTTP {(int)response.StatusCode}: {TrimForMessage(body)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return new PricingPage(
                ParsePricingRows(document.RootElement),
                ReadNextRequestUri(document.RootElement, baseApiUrl),
                ReadTotalPages(document.RootElement));
        }
    }

    private async Task UpsertCompanyPricesAsync(
        Guid organizationId,
        Guid supplierId,
        IReadOnlyCollection<PriceUpdate> updates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
        {
            return;
        }

        var supplierProductIds = updates.Select(x => x.SupplierProduct.Id).Distinct().ToArray();
        var prices = await dbContext.CompanySupplierPrices
            .IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && supplierProductIds.Contains(x.SupplierProductId))
            .ToDictionaryAsync(x => x.SupplierProductId, cancellationToken);
        foreach (var update in updates)
        {
            if (!prices.TryGetValue(update.SupplierProduct.Id, out var price))
            {
                price = new CompanySupplierPrice
                {
                    OrganizationId = organizationId,
                    SupplierId = supplierId,
                    SupplierProductId = update.SupplierProduct.Id,
                    SupplierSku = update.SupplierProduct.SupplierSku,
                    SourceSupplierProductId = update.SupplierProduct.SourceSupplierProductId,
                    ActualDealerCost = update.Row.ActualDealerCost!.Value,
                    LastSyncedAtUtc = now
                };
                dbContext.CompanySupplierPrices.Add(price);
                prices[update.SupplierProduct.Id] = price;
            }

            price.SupplierId = supplierId;
            price.SupplierSku = update.SupplierProduct.SupplierSku;
            price.SourceSupplierProductId = Clean(update.SupplierProduct.SourceSupplierProductId) ?? update.Row.SourceSupplierProductId;
            price.ActualDealerCost = update.Row.ActualDealerCost!.Value;
            price.Currency = Clean(update.Row.Currency) ?? "USD";
            price.EffectiveDate = update.Row.EffectiveDate;
            price.LastSyncedAtUtc = now;
            price.SourceDataJson = update.Row.SourceJson;
        }
    }

    private static IReadOnlyCollection<PricingRow> ParsePricingRows(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        if (data.ValueKind == JsonValueKind.Object)
        {
            return [ReadPricingRow(data)];
        }

        if (data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rows = new List<PricingRow>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                rows.Add(ReadPricingRow(item));
            }
        }

        return rows;
    }

    private static PricingRow ReadPricingRow(JsonElement item)
    {
        var attributes = item.TryGetProperty("attributes", out var attr) && attr.ValueKind == JsonValueKind.Object ? attr : item;
        var sourceId = ReadString(item, "id", "itemId", "item_id") ?? ReadString(attributes, "id", "itemId", "item_id");
        var supplierSku = ReadString(attributes, "partNumber", "part_number", "partnumber", "sku", "itemNumber", "item_number") ?? sourceId;
        var cost = ReadDecimal(attributes, "purchase_cost", "purchaseCost", "dealerCost", "dealer_cost", "actualDealerCost", "actual_dealer_cost", "cost");
        var currency = ReadString(attributes, "currency", "currencyCode", "currency_code");
        var effectiveDate = ReadDate(attributes, "effectiveDate", "effective_date");
        return new PricingRow(sourceId, supplierSku, cost, currency, effectiveDate, item.GetRawText());
    }

    private static Dictionary<string, SupplierProduct> BuildProductLookup(
        IEnumerable<SupplierProduct> supplierProducts,
        Func<SupplierProduct, string?> keySelector)
    {
        return supplierProducts
            .Select(x => new { Key = Clean(keySelector(x)), Product = x })
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Product, StringComparer.OrdinalIgnoreCase);
    }

    private static SupplierProduct? FindSupplierProduct(
        PricingRow row,
        IReadOnlyDictionary<string, SupplierProduct> productsBySourceId,
        IReadOnlyDictionary<string, SupplierProduct> productsBySku,
        IReadOnlyDictionary<string, SupplierProduct> productsBySupplierPartNumber)
    {
        if (Clean(row.SourceSupplierProductId) is { } sourceId &&
            (productsBySourceId.TryGetValue(sourceId, out var sourceMatch) ||
                productsBySku.TryGetValue(sourceId, out sourceMatch) ||
                productsBySupplierPartNumber.TryGetValue(sourceId, out sourceMatch)))
        {
            return sourceMatch;
        }

        if (Clean(row.SupplierSku) is { } supplierSku &&
            (productsBySku.TryGetValue(supplierSku, out var skuMatch) ||
                productsBySupplierPartNumber.TryGetValue(supplierSku, out skuMatch) ||
                productsBySourceId.TryGetValue(supplierSku, out skuMatch)))
        {
            return skuMatch;
        }

        return null;
    }

    private static string? ReadNextRequestUri(JsonElement root, string baseApiUrl)
    {
        if (!root.TryGetProperty("links", out var links) ||
            links.ValueKind != JsonValueKind.Object ||
            ReadString(links, "next") is not { } next)
        {
            return null;
        }

        if (Uri.TryCreate(next, UriKind.Absolute, out _))
        {
            return next;
        }

        return next.StartsWith("/", StringComparison.Ordinal)
            ? $"{baseApiUrl.TrimEnd('/')}{next}"
            : $"{baseApiUrl.TrimEnd('/')}/{next}";
    }

    private static Uri ResolveRequestUri(string baseApiUrl, string requestUri)
    {
        var cleanRequestUri = Clean(requestUri)
            ?? throw new InvalidOperationException("Turn14 pricing request URI was empty.");
        if (Uri.TryCreate(cleanRequestUri, UriKind.Absolute, out var absoluteUri) && IsHttpUri(absoluteUri))
        {
            return absoluteUri;
        }

        var cleanBaseApiUrl = Clean(baseApiUrl)
            ?? throw new InvalidOperationException("Turn14 base API URL was empty.");
        if (!Uri.TryCreate(EnsureTrailingSlash(cleanBaseApiUrl), UriKind.Absolute, out var baseUri) || !IsHttpUri(baseUri))
        {
            throw new InvalidOperationException($"Turn14 base API URL is not absolute: {cleanBaseApiUrl}");
        }

        var resolvedUri = new Uri(baseUri, cleanRequestUri);
        if (!resolvedUri.IsAbsoluteUri || !IsHttpUri(resolvedUri))
        {
            throw new InvalidOperationException($"Turn14 pricing request URI could not be resolved. Base '{cleanBaseApiUrl}', request '{cleanRequestUri}'.");
        }

        return resolvedUri;
    }

    private static string NormalizeBaseApiUrl(string baseApiUrl)
    {
        var cleanBaseApiUrl = Clean(baseApiUrl)
            ?? throw new InvalidOperationException("Turn14 base API URL was empty.");
        if (TryCreateHttpUri(cleanBaseApiUrl, out var absoluteUri))
        {
            return absoluteUri.ToString().TrimEnd('/');
        }

        if (!cleanBaseApiUrl.Contains("://", StringComparison.Ordinal) &&
            TryCreateHttpUri($"https://{cleanBaseApiUrl.TrimStart('/')}", out var inferredHttpsUri))
        {
            return inferredHttpsUri.ToString().TrimEnd('/');
        }

        throw new InvalidOperationException($"Turn14 base API URL is not absolute: {cleanBaseApiUrl}");
    }

    private static bool TryCreateHttpUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsedUri) && IsHttpUri(parsedUri))
        {
            uri = parsedUri;
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool IsHttpUri(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }

    private static int? ReadTotalPages(JsonElement root)
    {
        if (!root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryReadInt(meta, "total_pages", "totalPages", "last_page", "lastPage");
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

    private static int? TryReadInt(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
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

    private sealed record PricingPage(IReadOnlyCollection<PricingRow> Rows, string? NextRequestUri, int? TotalPages);

    private sealed record PricingRow(string? SourceSupplierProductId, string? SupplierSku, decimal? ActualDealerCost, string? Currency, DateOnly? EffectiveDate, string SourceJson);

    private sealed record PriceUpdate(SupplierProduct SupplierProduct, PricingRow Row);

    private sealed class Turn14AccessTokenProvider(Func<CancellationToken, Task<string>> requestTokenAsync)
    {
        private string? accessToken;

        public async Task<string> GetAsync(CancellationToken cancellationToken)
        {
            accessToken ??= await requestTokenAsync(cancellationToken);
            return accessToken;
        }

        public async Task<string> RefreshAsync(CancellationToken cancellationToken)
        {
            accessToken = await requestTokenAsync(cancellationToken);
            return accessToken;
        }
    }

    private sealed class Counters
    {
        public int RowsProcessed { get; set; }

        public int PricesUpserted { get; set; }

        public int UnmatchedRows { get; set; }
    }
}
