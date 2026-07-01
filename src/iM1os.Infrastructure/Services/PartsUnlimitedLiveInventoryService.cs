using System.Net;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class PartsUnlimitedLiveInventoryService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory) : IPartsUnlimitedLiveInventoryService
{
    private const int MaxBatchSize = 25;
    private const string SupplierCode = "PU";
    private const string DefaultBaseApiUrl = "https://api.parts-unlimited.com/api";

    public async Task<PartsUnlimitedLiveInventoryBatchResult> GetInventoryAsync(IReadOnlyCollection<Guid> supplierProductIds, CancellationToken cancellationToken)
    {
        return await GetInventoryInternalAsync(null, supplierProductIds, cancellationToken);
    }

    public async Task<PartsUnlimitedLiveInventoryBatchResult> GetInventoryForCompanyAsync(Guid organizationId, IReadOnlyCollection<Guid> supplierProductIds, CancellationToken cancellationToken)
    {
        return await GetInventoryInternalAsync(organizationId, supplierProductIds, cancellationToken);
    }

    private async Task<PartsUnlimitedLiveInventoryBatchResult> GetInventoryInternalAsync(Guid? organizationId, IReadOnlyCollection<Guid> supplierProductIds, CancellationToken cancellationToken)
    {
        var requestedIds = supplierProductIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(MaxBatchSize)
            .ToArray();
        if (requestedIds.Length == 0)
        {
            return new PartsUnlimitedLiveInventoryBatchResult(true, "No Parts Unlimited items were requested.", []);
        }

        var supplierProducts = await dbContext.SupplierProducts
            .Where(x => requestedIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (supplierProducts.Count == 0)
        {
            return Unavailable("Supplier items were not found.", []);
        }

        var supplierIds = supplierProducts.Select(x => x.SupplierId).Distinct().ToArray();
        var suppliers = await dbContext.Suppliers
            .AsNoTracking()
            .Where(x => supplierIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var partsUnlimitedProducts = supplierProducts
            .Where(x => suppliers.TryGetValue(x.SupplierId, out var supplier) && string.Equals(supplier.Code, SupplierCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (partsUnlimitedProducts.Count == 0)
        {
            return Unavailable("Live inventory is currently wired for Parts Unlimited items only.", BuildUnavailableItems(supplierProducts, "Not a Parts Unlimited item."));
        }

        var supplierId = partsUnlimitedProducts[0].SupplierId;
        var configuration = organizationId is null
            ? await dbContext.SupplierConnectorConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.SupplierId == supplierId && x.ConnectorKey == SupplierCode, cancellationToken)
            : null;
        var companyConfiguration = organizationId is null
            ? null
            : await dbContext.CompanySupplierConnectorConfigurations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrganizationId == organizationId.Value && x.SupplierId == supplierId && x.ConnectorKey == SupplierCode, cancellationToken);
        var apiKey = organizationId is null
            ? Clean(configuration?.ApiKey)
            : Clean(companyConfiguration?.ApiKey);
        var baseApiUrl = organizationId is null ? configuration?.BaseApiUrl : companyConfiguration?.BaseApiUrl;
        if ((organizationId is null && configuration is null) || (organizationId is not null && companyConfiguration is null))
        {
            return Unavailable("Parts Unlimited connector is not configured.", BuildUnavailableItems(partsUnlimitedProducts, "Parts Unlimited connector is not configured."));
        }

        if (apiKey is null)
        {
            return Unavailable("Parts Unlimited API key is required for live inventory.", BuildUnavailableItems(partsUnlimitedProducts, "Parts Unlimited API key is required."));
        }

        var partsByNumber = partsUnlimitedProducts
            .Select(x => new
            {
                Product = x,
                PartNumber = Clean(x.SupplierSku) ?? Clean(x.SupplierPartNumber)
            })
            .Where(x => x.PartNumber is not null)
            .GroupBy(x => x.PartNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(item => item.Product).ToList(), StringComparer.OrdinalIgnoreCase);
        if (partsByNumber.Count == 0)
        {
            return Unavailable("Parts Unlimited part numbers are missing for the requested items.", BuildUnavailableItems(partsUnlimitedProducts, "Part number is missing."));
        }

        baseApiUrl = Clean(baseApiUrl) ?? DefaultBaseApiUrl;
        baseApiUrl = baseApiUrl.TrimEnd('/');
        var escapedPartNumbers = partsByNumber.Keys
            .Select(Uri.EscapeDataString);
        var requestUri = $"{baseApiUrl}/v1/parts/inventory/{string.Join(",", escapedPartNumbers)}";
        var client = httpClientFactory.CreateClient("PartsUnlimitedApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var message = retryAfter is null
                ? "Parts Unlimited API rate limit reached. Try inventory lookup again later."
                : $"Parts Unlimited API rate limit reached. Try inventory lookup again after {retryAfter.Value.TotalSeconds:0} seconds.";
            return Unavailable(message, BuildUnavailableItems(partsUnlimitedProducts, message));
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = $"Parts Unlimited inventory request failed with HTTP {(int)response.StatusCode}.";
            return Unavailable(message, BuildUnavailableItems(partsUnlimitedProducts, message));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rowsByPartNumber = ParseInventory(document.RootElement);
        var items = new List<PartsUnlimitedPartInventoryResult>();
        foreach (var partNumberGroup in partsByNumber)
        {
            rowsByPartNumber.TryGetValue(partNumberGroup.Key, out var rows);
            rows ??= [];
            foreach (var supplierProduct in partNumberGroup.Value)
            {
                items.Add(new PartsUnlimitedPartInventoryResult(
                    supplierProduct.Id,
                    supplierProduct.SupplierSku,
                    true,
                    rows.Count == 0 ? "No Parts Unlimited warehouse inventory returned." : null,
                    rows));
            }
        }

        foreach (var nonPartsUnlimitedProduct in supplierProducts.Except(partsUnlimitedProducts))
        {
            items.Add(new PartsUnlimitedPartInventoryResult(
                nonPartsUnlimitedProduct.Id,
                nonPartsUnlimitedProduct.SupplierSku,
                false,
                "Not a Parts Unlimited item.",
                []));
        }

        return new PartsUnlimitedLiveInventoryBatchResult(true, null, items);
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<PartsUnlimitedWarehouseInventoryRow>> ParseInventory(JsonElement root)
    {
        var rowsByPartNumber = new Dictionary<string, IReadOnlyCollection<PartsUnlimitedWarehouseInventoryRow>>(StringComparer.OrdinalIgnoreCase);
        var data = SelectInventoryData(root);
        if (data.ValueKind == JsonValueKind.Object && !LooksLikeInventoryItem(data))
        {
            foreach (var property in data.EnumerateObject())
            {
                if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                {
                    continue;
                }

                var rows = ParseWarehouseRows(property.Value);
                if (rows.Count > 0)
                {
                    rowsByPartNumber[property.Name] = rows;
                }
            }
        }

        var items = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToArray()
            : data.ValueKind == JsonValueKind.Object ? [data] : [];
        foreach (var item in items)
        {
            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;
            var partNumber = FirstString(attributes, "part_number", "partNumber", "partnumber", "part", "sku", "PartNumber") ??
                FirstString(item, "part_number", "partNumber", "partnumber", "part", "sku", "id");
            if (partNumber is null)
            {
                continue;
            }

            rowsByPartNumber[partNumber] = ParseWarehouseRows(attributes);
        }

        return rowsByPartNumber;
    }

    private static JsonElement SelectInventoryData(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "availability", "inventory", "inventories" })
            {
                if (data.TryGetProperty(propertyName, out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    return nested;
                }
            }
        }

        return data;
    }

    private static bool LooksLikeInventoryItem(JsonElement item)
    {
        return FirstString(item, "part_number", "partNumber", "partnumber", "part", "sku", "id") is not null ||
            item.TryGetProperty("attributes", out _);
    }

    private static IReadOnlyCollection<PartsUnlimitedWarehouseInventoryRow> ParseWarehouseRows(JsonElement attributes)
    {
        if (attributes.ValueKind == JsonValueKind.Array)
        {
            return ParseWarehouseContainer(attributes)
                .OrderBy(x => x.WarehouseCode)
                .ToList();
        }

        if (attributes.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var rows = new List<PartsUnlimitedWarehouseInventoryRow>();
        foreach (var containerName in new[] { "inventory", "warehouses", "warehouse_inventory", "warehouseInventory", "availability" })
        {
            if (!attributes.TryGetProperty(containerName, out var container))
            {
                continue;
            }

            rows.AddRange(ParseWarehouseContainer(container));
        }

        rows.AddRange(ParseFlatWarehouseRows(attributes));
        return rows
            .GroupBy(x => x.WarehouseCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.WarehouseCode)
            .ToList();
    }

    private static IReadOnlyCollection<PartsUnlimitedWarehouseInventoryRow> ParseWarehouseContainer(JsonElement container)
    {
        if (container.ValueKind == JsonValueKind.Array)
        {
            return container
                .EnumerateArray()
                .Select(ParseWarehouseObject)
                .Where(row => row is not null)
                .Select(row => row!)
                .ToList();
        }

        if (container.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var directRow = ParseWarehouseObject(container);
        if (directRow is not null)
        {
            return [directRow];
        }

        var rows = new List<PartsUnlimitedWarehouseInventoryRow>();
        foreach (var property in container.EnumerateObject())
        {
            var warehouseCode = property.Name;
            var quantity = IntValue(property.Value);
            var quantityDisplay = QuantityDisplay(property.Value) ?? (quantity is null ? "-" : quantity.Value.ToString());
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                quantity = FirstInt(property.Value, "quantity", "qty", "available", "available_quantity", "availableQuantity", "on_hand", "onHand");
                quantityDisplay = FirstString(property.Value, "quantity_display", "quantityDisplay", "available_display", "availableDisplay") ??
                    (quantity is null ? "-" : quantity.Value.ToString());
            }

            rows.Add(new PartsUnlimitedWarehouseInventoryRow(
                warehouseCode,
                WarehouseDisplayName(warehouseCode),
                quantity,
                quantityDisplay));
        }

        return rows;
    }

    private static PartsUnlimitedWarehouseInventoryRow? ParseWarehouseObject(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var warehouseCode = FirstString(item, "warehouse_code", "warehouseCode", "warehouse", "code", "location", "location_id", "locationId", "name");
        var quantity = FirstInt(item, "quantity", "qty", "available", "available_quantity", "availableQuantity", "on_hand", "onHand");
        if (warehouseCode is null && quantity is null)
        {
            return null;
        }

        warehouseCode ??= "Unknown";
        var quantityDisplay = FirstString(item, "quantity_display", "quantityDisplay", "available_display", "availableDisplay") ??
            (quantity is null ? "-" : quantity.Value.ToString());
        var warehouseName = FirstString(item, "warehouse_name", "warehouseName", "display_name", "displayName") ??
            WarehouseDisplayName(warehouseCode);
        return new PartsUnlimitedWarehouseInventoryRow(warehouseCode, warehouseName, quantity, quantityDisplay);
    }

    private static IReadOnlyCollection<PartsUnlimitedWarehouseInventoryRow> ParseFlatWarehouseRows(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var rows = new List<PartsUnlimitedWarehouseInventoryRow>();
        foreach (var property in attributes.EnumerateObject())
        {
            if (!property.Name.EndsWith("_warehouse", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.EndsWith("Warehouse", StringComparison.Ordinal))
            {
                continue;
            }

            var warehouseCode = property.Name.EndsWith("_warehouse", StringComparison.OrdinalIgnoreCase)
                ? property.Name[..^"_warehouse".Length]
                : property.Name[..^"Warehouse".Length];
            warehouseCode = warehouseCode.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            var quantity = IntValue(property.Value);
            var quantityDisplay = QuantityDisplay(property.Value) ?? (quantity is null ? "-" : quantity.Value.ToString());
            rows.Add(new PartsUnlimitedWarehouseInventoryRow(
                warehouseCode,
                WarehouseDisplayName(warehouseCode),
                quantity,
                quantityDisplay));
        }

        return rows;
    }

    private static IReadOnlyCollection<PartsUnlimitedPartInventoryResult> BuildUnavailableItems(IEnumerable<SupplierProduct> supplierProducts, string message)
    {
        return supplierProducts
            .Select(x => new PartsUnlimitedPartInventoryResult(x.Id, x.SupplierSku, false, message, []))
            .ToList();
    }

    private static PartsUnlimitedLiveInventoryBatchResult Unavailable(string message, IReadOnlyCollection<PartsUnlimitedPartInventoryResult> items)
    {
        return new PartsUnlimitedLiveInventoryBatchResult(false, message, items);
    }

    private static string WarehouseDisplayName(string warehouseCode)
    {
        return warehouseCode.Equals("total", StringComparison.OrdinalIgnoreCase)
            ? "Total"
            : $"{warehouseCode.ToUpperInvariant()} Warehouse";
    }

    private static string? FirstString(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
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

        return null;
    }

    private static int? FirstInt(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            var parsed = IntValue(value);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? IntValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static string? QuantityDisplay(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return Clean(value.GetString());
        }

        return null;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
