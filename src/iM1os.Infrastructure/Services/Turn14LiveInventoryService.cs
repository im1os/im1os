using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class Turn14LiveInventoryService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider clock) : ITurn14LiveInventoryService
{
    private const string Turn14SupplierCode = "TURN14";
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? cachedApiClientId;
    private static string? cachedAccessToken;
    private static DateTimeOffset cachedAccessTokenExpiresAtUtc;

    public async Task<Turn14LiveInventoryResult> GetInventoryAsync(Guid supplierProductId, CancellationToken cancellationToken)
    {
        return await GetInventoryInternalAsync(null, supplierProductId, cancellationToken);
    }

    public async Task<Turn14LiveInventoryResult> GetInventoryForCompanyAsync(Guid organizationId, Guid supplierProductId, CancellationToken cancellationToken)
    {
        return await GetInventoryInternalAsync(organizationId, supplierProductId, cancellationToken);
    }

    private async Task<Turn14LiveInventoryResult> GetInventoryInternalAsync(Guid? organizationId, Guid supplierProductId, CancellationToken cancellationToken)
    {
        var supplierProduct = await dbContext.SupplierProducts
            .SingleOrDefaultAsync(x => x.Id == supplierProductId, cancellationToken);
        if (supplierProduct is null)
        {
            return Unavailable("Supplier item was not found.");
        }

        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == supplierProduct.SupplierId, cancellationToken);
        if (!string.Equals(supplier.Code, Turn14SupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            return Unavailable("Live inventory is currently wired for Turn14 items only.");
        }

        var itemId = Clean(supplierProduct.SourceSupplierProductId);
        if (itemId is null)
        {
            return Unavailable("Turn14 item id is not available yet. Re-run the Turn14 product loadsheet import with API credentials configured.");
        }

        var configuration = organizationId is null
            ? await dbContext.SupplierConnectorConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == Turn14SupplierCode, cancellationToken)
            : null;
        var companyConfiguration = organizationId is null
            ? null
            : await dbContext.CompanySupplierConnectorConfigurations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrganizationId == organizationId.Value && x.SupplierId == supplier.Id && x.ConnectorKey == Turn14SupplierCode, cancellationToken);
        if ((organizationId is null && configuration is null) || (organizationId is not null && companyConfiguration is null))
        {
            return Unavailable("Turn14 connector is not configured.");
        }

        var platformSecrets = organizationId is null && configuration is not null
            ? Turn14ConnectorSecrets.FromStoredConfiguration(configuration)
            : null;
        var apiClientId = organizationId is null ? platformSecrets?.ApiClientId : companyConfiguration?.ApiKey;
        var apiClientSecret = organizationId is null ? platformSecrets?.ApiClientSecret : companyConfiguration?.ApiSecretProtected;
        if (string.IsNullOrWhiteSpace(apiClientId) || string.IsNullOrWhiteSpace(apiClientSecret))
        {
            return Unavailable("Turn14 API client id and client secret are required for live inventory.");
        }

        var tokenResult = await GetApiAccessTokenAsync(apiClientId.Trim(), apiClientSecret.Trim(), cancellationToken);
        if (tokenResult.Token is null)
        {
            return Unavailable(tokenResult.Message ?? "Turn14 API token is unavailable.");
        }

        var client = httpClientFactory.CreateClient("Turn14Api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);
        using var response = await client.GetAsync($"https://api.turn14.com/v1/inventory/{Uri.EscapeDataString(itemId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            return Unavailable(retryAfter is null
                ? "Turn14 API rate limit reached. Try inventory lookup again later."
                : $"Turn14 API rate limit reached. Try inventory lookup again after {retryAfter.Value.TotalSeconds:0} seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (CachedWarehouseAvailabilityRows(supplierProduct.WarehouseAvailability) is { Count: > 0 } cachedRows)
                {
                    return new Turn14LiveInventoryResult(
                        true,
                        "No live Turn14 warehouse inventory was returned. Showing last imported warehouse availability.",
                        cachedRows);
                }

                return new Turn14LiveInventoryResult(
                    true,
                    "No Turn14 warehouse inventory was returned.",
                    []);
            }

            return Unavailable($"Turn14 inventory request failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rows = ParseInventory(document.RootElement);
        return rows.Count == 0
            ? new Turn14LiveInventoryResult(true, "No Turn14 warehouse inventory was returned.", [])
            : new Turn14LiveInventoryResult(true, null, rows);
    }

    private async Task<TokenLookupResult> GetApiAccessTokenAsync(string apiClientId, string apiClientSecret, CancellationToken cancellationToken)
    {
        if (HasUsableCachedToken(apiClientId))
        {
            return new TokenLookupResult(cachedAccessToken, null);
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (HasUsableCachedToken(apiClientId))
            {
                return new TokenLookupResult(cachedAccessToken, null);
            }

            return await RequestApiAccessTokenAsync(apiClientId, apiClientSecret, cancellationToken);
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private async Task<TokenLookupResult> RequestApiAccessTokenAsync(string apiClientId, string apiClientSecret, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Turn14Api");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = apiClientId,
            ["client_secret"] = apiClientSecret
        });
        using var response = await client.PostAsync("https://api.turn14.com/v1/token", content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            return new TokenLookupResult(null, retryAfter is null
                ? "Turn14 API rate limit reached while requesting an access token. Try inventory lookup again later."
                : $"Turn14 API rate limit reached while requesting an access token. Try inventory lookup again after {retryAfter.Value.TotalSeconds:0} seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new TokenLookupResult(null, $"Turn14 API token request failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("access_token", out var tokenElement) ||
            tokenElement.ValueKind != JsonValueKind.String ||
            Clean(tokenElement.GetString()) is not { } token)
        {
            return new TokenLookupResult(null, "Turn14 token response did not include an access token.");
        }

        var expiresInSeconds = document.RootElement.TryGetProperty("expires_in", out var expiresInElement) &&
            expiresInElement.ValueKind == JsonValueKind.Number &&
            expiresInElement.TryGetInt32(out var parsedExpiresIn)
                ? parsedExpiresIn
                : 3600;
        cachedApiClientId = apiClientId;
        cachedAccessToken = token;
        cachedAccessTokenExpiresAtUtc = clock.UtcNow.AddSeconds(Math.Max(60, expiresInSeconds - 120));
        return new TokenLookupResult(token, null);
    }

    private bool HasUsableCachedToken(string apiClientId)
    {
        return cachedAccessToken is not null &&
            string.Equals(cachedApiClientId, apiClientId, StringComparison.Ordinal) &&
            cachedAccessTokenExpiresAtUtc > clock.UtcNow;
    }

    private static IReadOnlyCollection<Turn14WarehouseInventoryRow> ParseInventory(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        var items = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToArray()
            : data.ValueKind == JsonValueKind.Object ? [data] : [];
        var rows = new List<Turn14WarehouseInventoryRow>();
        foreach (var item in items)
        {
            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;
            if (attributes.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Object)
            {
                foreach (var location in inventory.EnumerateObject())
                {
                    var quantity = IntValue(location.Value);
                    rows.Add(new Turn14WarehouseInventoryRow(
                        location.Name,
                        WarehouseDisplayName(location.Name),
                        quantity,
                        quantity is null ? "-" : quantity.Value.ToString()));
                }
            }

            if (attributes.TryGetProperty("manufacturer", out var manufacturer) && manufacturer.ValueKind == JsonValueKind.Object)
            {
                var stock = manufacturer.TryGetProperty("stock", out var stockValue) ? IntValue(stockValue) : null;
                var esd = manufacturer.TryGetProperty("esd", out var esdValue) && esdValue.ValueKind == JsonValueKind.String
                    ? Clean(esdValue.GetString())
                    : null;
                if (stock is not null || esd is not null)
                {
                    rows.Add(new Turn14WarehouseInventoryRow(
                        "MFR",
                        "Manufacturer",
                        stock,
                        esd is null ? stock?.ToString() ?? "-" : $"{stock?.ToString() ?? "-"} / ESD {esd}"));
                }
            }
        }

        return rows.OrderBy(x => x.WarehouseCode).ToList();
    }

    private static IReadOnlyCollection<Turn14WarehouseInventoryRow> CachedWarehouseAvailabilityRows(string? warehouseAvailabilityJson)
    {
        if (string.IsNullOrWhiteSpace(warehouseAvailabilityJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(warehouseAvailabilityJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("warehouse_availability", out var nestedAvailability))
            {
                root = nestedAvailability;
            }

            var rows = new List<Turn14WarehouseInventoryRow>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("location_id", out var locationElement) ||
                        Clean(locationElement.GetString()) is not { } locationId)
                    {
                        continue;
                    }

                    var canPlaceOrder = item.TryGetProperty("can_place_order", out var canPlaceOrderElement) &&
                        canPlaceOrderElement.ValueKind == JsonValueKind.True;
                    rows.Add(new Turn14WarehouseInventoryRow(
                        locationId,
                        WarehouseDisplayName(locationId),
                        null,
                        canPlaceOrder ? "Available" : "Unavailable"));
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var location in root.EnumerateObject())
                {
                    var available = location.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Object when location.Value.TryGetProperty("can_place_order", out var canPlaceOrderElement) =>
                            canPlaceOrderElement.ValueKind == JsonValueKind.True,
                        _ => false
                    };
                    rows.Add(new Turn14WarehouseInventoryRow(
                        location.Name,
                        WarehouseDisplayName(location.Name),
                        null,
                        available ? "Available" : "Unavailable"));
                }
            }

            return rows.OrderBy(x => x.WarehouseCode).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string WarehouseDisplayName(string warehouseCode)
    {
        return warehouseCode switch
        {
            "01" => "Turn14 East",
            "02" => "Turn14 West",
            "03" => "Turn14 Midwest",
            "59" => "Turn14 Central",
            _ => $"Turn14 {warehouseCode}"
        };
    }

    private static int? IntValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static Turn14LiveInventoryResult Unavailable(string message)
    {
        return new Turn14LiveInventoryResult(false, message, []);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record TokenLookupResult(string? Token, string? Message);
}
