using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class Turn14MediaEnrichmentService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider clock,
    ILogger<Turn14MediaEnrichmentService> logger) : ITurn14MediaEnrichmentService
{
    private const string Turn14SupplierCode = "TURN14";
    private static readonly TimeSpan DefaultRateLimitBackoff = TimeSpan.FromSeconds(60);

    public async Task<Turn14MediaEnrichmentRunResult> ImportAsync(Turn14MediaEnrichmentRunRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);
        var configuration = await dbContext.SupplierConnectorConfigurations
            .SingleAsync(x => x.Id == importRun.SupplierConnectorConfigurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);

        if (!string.Equals(supplier.Code, Turn14SupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Import run {request.ImportRunId} is not a Turn14 media enrichment run.");
        }

        var secrets = Turn14ConnectorSecrets.FromConfiguration(configuration);
        if (!secrets.HasApiCredentials)
        {
            throw new InvalidOperationException("Turn14 API client id and client secret are required for media enrichment.");
        }

        var delayMilliseconds = Math.Clamp(request.DelayMilliseconds, 0, 5000);
        var maxItems = request.MaxItems;
        // SupplierImagesJson is jsonb in PostgreSQL; comparing it to an empty string makes PostgreSQL parse '' as JSON.
        var query = dbContext.SupplierProducts
            .Where(x =>
                x.SupplierId == supplier.Id &&
                x.SupplierImagesJson == null)
            .OrderBy(x => x.SupplierSku)
            .AsQueryable();
        if (maxItems is not null)
        {
            query = query.Take(maxItems.Value);
        }

        var candidates = await query.ToListAsync(cancellationToken);
        importRun.Status = "Running";
        importRun.StartedAtUtc = clock.UtcNow;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = candidates.Count;
        importRun.Message = $"Turn14 media enrichment started for {candidates.Count} products.";
        await dbContext.SaveChangesAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            importRun.Status = "Completed";
            importRun.CompletedAtUtc = clock.UtcNow;
            importRun.Message = "Turn14 media enrichment completed. No products need enrichment.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Turn14MediaEnrichmentRunResult(importRun.Id, 0, 0, 0, false);
        }

        var tokenProvider = new Turn14AccessTokenProvider(tokenCancellationToken => GetApiAccessTokenAsync(secrets, tokenCancellationToken));
        var processed = 0;
        var updated = 0;
        var skipped = 0;
        var stoppedForRateLimit = false;
        try
        {
            var bulkEnrichment = await EnrichFromItemsIndexAsync(tokenProvider, candidates, importRun, delayMilliseconds, cancellationToken);
            processed += bulkEnrichment.ImagesUpdated;
            updated += bulkEnrichment.ImagesUpdated;
            skipped += candidates.Count - bulkEnrichment.ImagesUpdated;
            importRun.ProgressProcessed = processed;
            importRun.ProgressTotal = candidates.Count;
            importRun.Message = $"Turn14 media enrichment bulk-filled {bulkEnrichment.ImagesUpdated:N0} thumbnails and resolved {bulkEnrichment.ItemIdsResolved:N0} item ids. Detail media fetch skipped.";
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Turn14RateLimitException exception)
        {
            logger.LogWarning(exception, "Turn14 media enrichment paused by API rate limit while scanning items index.");
            stoppedForRateLimit = true;
        }

        importRun.Status = "Completed";
        importRun.CompletedAtUtc = clock.UtcNow;
        importRun.ProgressProcessed = processed;
        importRun.Message = stoppedForRateLimit
            ? $"Turn14 media enrichment paused by API rate limit after {processed} products. Updated {updated}, skipped {skipped}. It will resume on the next run."
            : $"Turn14 media enrichment completed using items-index thumbnails. Processed {processed}, updated {updated}, skipped {skipped}.";
        await dbContext.SaveChangesAsync(cancellationToken);

        return new Turn14MediaEnrichmentRunResult(importRun.Id, processed, updated, skipped, stoppedForRateLimit);
    }

    private async Task<Turn14ItemsIndexEnrichmentResult> EnrichFromItemsIndexAsync(
        Turn14AccessTokenProvider tokenProvider,
        IReadOnlyCollection<SupplierProduct> supplierProducts,
        SupplierConnectorImportRun importRun,
        int delayMilliseconds,
        CancellationToken cancellationToken)
    {
        var pendingSkus = supplierProducts
            .Select(x => x.SupplierSku)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (pendingSkus.Count == 0)
        {
            return new Turn14ItemsIndexEnrichmentResult(0, 0);
        }

        var productsBySku = supplierProducts
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierSku))
            .GroupBy(x => x.SupplierSku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient("Turn14Api");
        var page = 1;
        var totalPages = 1;
        var itemIdsResolved = 0;
        var imagesUpdated = 0;

        while (page <= totalPages && pendingSkus.Count > 0)
        {
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }

            using var response = await GetTurn14ApiAsync(client, tokenProvider, $"https://api.turn14.com/v1/items?page={page}", cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            totalPages = ReadInt(document.RootElement, "meta", "total_pages") ?? totalPages;
            if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                var thumbnailUpdates = new List<(SupplierProduct Product, string ImageJson)>();
                foreach (var item in data.EnumerateArray())
                {
                    var apiItem = Turn14ApiItem.FromJson(item);
                    if (string.IsNullOrWhiteSpace(apiItem.Id) ||
                        string.IsNullOrWhiteSpace(apiItem.PartNumber) ||
                        !pendingSkus.Contains(apiItem.PartNumber) ||
                        !productsBySku.TryGetValue(apiItem.PartNumber, out var products))
                    {
                        continue;
                    }

                    foreach (var product in products)
                    {
                        if (string.IsNullOrWhiteSpace(product.SourceSupplierProductId))
                        {
                            product.SourceSupplierProductId = apiItem.Id;
                            itemIdsResolved++;
                        }

                        if (product.SupplierImagesJson is null &&
                            MediaJson(apiItem.Thumbnail) is { } imageJson)
                        {
                            product.SupplierImagesJson = imageJson;
                            thumbnailUpdates.Add((product, imageJson));
                        }

                        if (product.SourceSupplierProductId == apiItem.Id || product.SupplierImagesJson is not null)
                        {
                            product.LastSyncedAtUtc = clock.UtcNow;
                        }
                    }

                    pendingSkus.Remove(apiItem.PartNumber);
                }

                imagesUpdated += await ApplyThumbnailUpdatesAsync(thumbnailUpdates, cancellationToken);
            }

            importRun.ProgressProcessed = page;
            importRun.ProgressTotal = totalPages;
            importRun.Message = $"Scanning Turn14 items index page {page:N0} / {totalPages:N0}; resolved {itemIdsResolved:N0} item ids and bulk-filled {imagesUpdated:N0} thumbnails.";
            await dbContext.SaveChangesAsync(cancellationToken);
            page++;
        }

        return new Turn14ItemsIndexEnrichmentResult(itemIdsResolved, imagesUpdated);
    }

    private async Task<int> ApplyThumbnailUpdatesAsync(
        IReadOnlyCollection<(SupplierProduct Product, string ImageJson)> updates,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
        {
            return 0;
        }

        var globalProductIds = updates
            .Select(x => x.Product.GlobalProductId)
            .Distinct()
            .ToList();
        var globalProductsById = await dbContext.GlobalProducts
            .Where(x => globalProductIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var (product, imageJson) in updates)
        {
            if (globalProductsById.TryGetValue(product.GlobalProductId, out var globalProduct))
            {
                globalProduct.ImagesJson = imageJson;
            }
        }

        return updates.Count;
    }

    private async Task<string> GetApiAccessTokenAsync(Turn14ConnectorSecrets secrets, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Turn14Api");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = secrets.ApiClientId!,
            ["client_secret"] = secrets.ApiClientSecret!
        });
        using var response = await client.PostAsync("https://api.turn14.com/v1/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty("access_token", out var token) && token.ValueKind == JsonValueKind.String
            ? token.GetString() ?? throw new InvalidOperationException("Turn14 token response did not include an access token.")
            : throw new InvalidOperationException("Turn14 token response did not include an access token.");
    }

    private async Task<Turn14ItemData?> GetItemDataAsync(Turn14AccessTokenProvider tokenProvider, string itemId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Turn14Api");
        using var response = await GetTurn14ApiAsync(client, tokenProvider, $"https://api.turn14.com/v1/items/data/{Uri.EscapeDataString(itemId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            return null;
        }

        var item = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().FirstOrDefault()
            : data;
        return item.ValueKind == JsonValueKind.Object ? Turn14ItemData.FromJson(item) : null;
    }

    private static async Task<HttpResponseMessage> GetTurn14ApiAsync(
        HttpClient client,
        Turn14AccessTokenProvider tokenProvider,
        string requestUrl,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var refreshedForUnauthorized = false;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetAsync(cancellationToken));
            var response = await client.SendAsync(httpRequest, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedForUnauthorized)
            {
                response.Dispose();
                await tokenProvider.RefreshAsync(cancellationToken);
                refreshedForUnauthorized = true;
                attempt--;
                continue;
            }

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                response.EnsureSuccessStatusCode();
                return response;
            }

            var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRateLimitBackoff;
            response.Dispose();
            if (attempt == maxAttempts)
            {
                throw new Turn14RateLimitException($"Turn14 API rate limit reached after {maxAttempts} attempts.");
            }

            await Task.Delay(retryAfter, cancellationToken);
        }

        throw new Turn14RateLimitException("Turn14 API rate limit reached.");
    }

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

    private static string? MediaJson(IReadOnlyCollection<Turn14Image> images)
    {
        return images.Count == 0
            ? null
            : JsonSerializer.Serialize(images.Select((image, index) => new
            {
                image.Url,
                image.Width,
                image.Height,
                image.Size,
                image.MediaContent,
                isPrimary = index == 0 || image.MediaContent?.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true
            }));
    }

    private static string? MediaJson(string? primaryImage)
    {
        return Clean(primaryImage) is not { } imageUrl
            ? null
            : JsonSerializer.Serialize(new[] { new { url = imageUrl, isPrimary = true } });
    }

    private sealed record Turn14ItemsIndexEnrichmentResult(int ItemIdsResolved, int ImagesUpdated);

    private sealed record Turn14ApiItem(string Id, string? PartNumber, string? Thumbnail)
    {
        public static Turn14ApiItem FromJson(JsonElement item)
        {
            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;

            return new Turn14ApiItem(
                StringValue(item, "id") ?? string.Empty,
                StringValue(attributes, "part_number"),
                StringValue(attributes, "thumbnail"));
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

    private static int? ReadInt(JsonElement root, string objectProperty, string propertyName)
    {
        if (!root.TryGetProperty(objectProperty, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.Any(char.IsControl))
        {
            return trimmed;
        }

        return new string(trimmed.Where(x => !char.IsControl(x)).ToArray());
    }

    private static string? SanitizeJsonForPostgres(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : json.Replace("\\u0000", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Turn14RateLimitException(string message) : Exception(message);
}
