using System.Net.Http.Headers;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class WpsLiveInventoryService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory) : IWpsLiveInventoryService
{
    private const string WpsSupplierCode = "WPS";
    private const string DefaultBaseApiUrl = "https://api.wps-inc.com";

    public async Task<WpsLiveInventoryResult> GetInventoryAsync(Guid supplierProductId, CancellationToken cancellationToken)
    {
        return await GetInventoryInternalAsync(null, supplierProductId, cancellationToken);
    }

    public async Task<WpsLiveInventoryResult> GetInventoryForCompanyAsync(Guid organizationId, Guid supplierProductId, CancellationToken cancellationToken)
    {
        return await GetInventoryInternalAsync(organizationId, supplierProductId, cancellationToken);
    }

    private async Task<WpsLiveInventoryResult> GetInventoryInternalAsync(Guid? organizationId, Guid supplierProductId, CancellationToken cancellationToken)
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
        if (!string.Equals(supplier.Code, WpsSupplierCode, StringComparison.OrdinalIgnoreCase))
        {
            return Unavailable("Live inventory is currently wired for WPS items only.");
        }

        var configuration = organizationId is null
            ? await dbContext.SupplierConnectorConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ConnectorKey == WpsSupplierCode, cancellationToken)
            : null;
        var companyConfiguration = organizationId is null
            ? null
            : await dbContext.CompanySupplierConnectorConfigurations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrganizationId == organizationId.Value && x.SupplierId == supplier.Id && x.ConnectorKey == WpsSupplierCode, cancellationToken);
        var apiKey = organizationId is null ? configuration?.ApiKey : companyConfiguration?.ApiKey;
        var baseApiUrl = organizationId is null ? configuration?.BaseApiUrl : companyConfiguration?.BaseApiUrl;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unavailable("WPS API key is required for live inventory.");
        }

        var client = httpClientFactory.CreateClient("WpsDataDepot");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        var baseUrl = string.IsNullOrWhiteSpace(baseApiUrl) ? DefaultBaseApiUrl : baseApiUrl.TrimEnd('/');

        SkuInventoryLookup? skuInventory = null;
        var sourceItemId = Clean(supplierProduct.SourceSupplierProductId);
        if (sourceItemId is null)
        {
            skuInventory = await GetInventoryBySkuAsync(client, baseUrl, supplierProduct, cancellationToken);
            if (!string.IsNullOrWhiteSpace(skuInventory.SourceItemId))
            {
                sourceItemId = skuInventory.SourceItemId;
                supplierProduct.SourceSupplierProductId = sourceItemId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (skuInventory.Warehouses.Count > 0)
            {
                return new WpsLiveInventoryResult(true, null, skuInventory.Warehouses);
            }
        }

        sourceItemId ??= await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x => x.SupplierProductId == supplierProduct.Id)
            .Select(x => x.SourceSupplierProductId ?? x.SourceFitmentItemId)
            .FirstOrDefaultAsync(x => x != null, cancellationToken);
        sourceItemId ??= await dbContext.SupplierFitmentRecords
            .AsNoTracking()
            .Where(x => x.SupplierId == supplier.Id && x.SupplierSku == supplierProduct.SupplierSku)
            .Select(x => x.SourceSupplierProductId ?? x.SourceFitmentItemId)
            .FirstOrDefaultAsync(x => x != null, cancellationToken);
        if (string.IsNullOrWhiteSpace(sourceItemId))
        {
            return Unavailable(skuInventory?.Message ?? "WPS item id is not available yet.");
        }

        var requestUrl = $"{baseUrl}/inventory?filter[item_id]={Uri.EscapeDataString(sourceItemId)}";
        using var response = await client.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Unavailable($"WPS inventory request failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var warehouses = ParseInventory(document.RootElement);
        return warehouses.Count == 0
            ? new WpsLiveInventoryResult(true, "No warehouse inventory was returned by WPS.", [])
            : new WpsLiveInventoryResult(true, null, warehouses);
    }

    private static async Task<SkuInventoryLookup> GetInventoryBySkuAsync(HttpClient client, string baseUrl, SupplierProduct supplierProduct, CancellationToken cancellationToken)
    {
        var requestUrl = $"{baseUrl}/items?filter[sku]={Uri.EscapeDataString(supplierProduct.SupplierSku)}&include=inventory";
        using var response = await client.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new SkuInventoryLookup(null, [], $"WPS item lookup failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var sourceItemId = FindItemId(document.RootElement, supplierProduct.SupplierSku);
        var warehouses = ParseInventory(document.RootElement);
        return warehouses.Count == 0
            ? new SkuInventoryLookup(sourceItemId, [], "No WPS warehouse inventory was returned for this SKU.")
            : new SkuInventoryLookup(sourceItemId, warehouses, null);
    }

    private static IReadOnlyCollection<WpsWarehouseInventoryRow> ParseInventory(JsonElement root)
    {
        var warehouseNames = ParseIncludedWarehouseNames(root);
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        var items = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToArray()
            : data.ValueKind == JsonValueKind.Object ? [data] : [];

        return items
            .SelectMany(item => ParseInventoryRows(item, warehouseNames))
            .Concat(ParseIncludedInventory(root, warehouseNames))
            .Where(row => row is not null)
            .Select(row => row!)
            .OrderBy(row => row.WarehouseCode)
            .ToList();
    }

    private static IReadOnlyCollection<WpsWarehouseInventoryRow?> ParseInventoryRows(JsonElement item, IReadOnlyDictionary<string, string> warehouseNames)
    {
        var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
            ? attributesElement
            : item;
        var flatWarehouseRows = ParseFlatWarehouseRows(attributes);
        if (flatWarehouseRows.Count > 0)
        {
            return flatWarehouseRows;
        }

        var warehouseCode = FirstString(attributes, "warehouse_code", "warehouseCode", "warehouse_id", "warehouseId", "warehouse");
        if (warehouseCode is null &&
            item.TryGetProperty("relationships", out var relationships) &&
            relationships.TryGetProperty("warehouse", out var warehouseRelationship) &&
            warehouseRelationship.TryGetProperty("data", out var warehouseData))
        {
            warehouseCode = FirstString(warehouseData, "id");
        }

        var quantity = FirstInt(attributes, "quantity", "qty", "available", "available_quantity", "availableQuantity");
        if (warehouseCode is null && quantity is null)
        {
            return [];
        }

        warehouseCode ??= "Unknown";
        var quantityDisplay = FirstString(attributes, "quantity_display", "quantityDisplay", "available_display", "availableDisplay") ??
            (quantity is null ? "-" : quantity >= 25 ? "25+" : quantity.Value.ToString());
        var warehouseName = FirstString(attributes, "warehouse_name", "warehouseName", "name") ??
            (warehouseNames.TryGetValue(warehouseCode, out var includedName) ? includedName : warehouseCode);

        return [new WpsWarehouseInventoryRow(warehouseCode, warehouseName, quantity, quantityDisplay)];
    }

    private static IReadOnlyCollection<WpsWarehouseInventoryRow?> ParseIncludedInventory(JsonElement root, IReadOnlyDictionary<string, string> warehouseNames)
    {
        if (!root.TryGetProperty("included", out var included) || included.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return included
            .EnumerateArray()
            .Where(item =>
            {
                var type = FirstString(item, "type");
                return string.Equals(type, "inventory", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "inventories", StringComparison.OrdinalIgnoreCase);
            })
            .SelectMany(item => ParseInventoryRows(item, warehouseNames))
            .ToList();
    }

    private static IReadOnlyCollection<WpsWarehouseInventoryRow> ParseFlatWarehouseRows(JsonElement attributes)
    {
        var rows = new List<WpsWarehouseInventoryRow>();
        foreach (var property in attributes.EnumerateObject())
        {
            if (!property.Name.EndsWith("_warehouse", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var warehouseCode = property.Name[..^"_warehouse".Length].ToUpperInvariant();
            int? quantity = null;
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number))
            {
                quantity = number;
            }
            else if (property.Value.ValueKind == JsonValueKind.String && int.TryParse(property.Value.GetString(), out var parsed))
            {
                quantity = parsed;
            }

            rows.Add(new WpsWarehouseInventoryRow(
                warehouseCode,
                WarehouseDisplayName(warehouseCode),
                quantity,
                quantity is null ? "-" : quantity >= 25 ? "25+" : quantity.Value.ToString()));
        }

        return rows;
    }

    private static string WarehouseDisplayName(string warehouseCode)
    {
        return warehouseCode switch
        {
            "CA" => "CA Warehouse",
            "GA" => "GA Warehouse",
            "ID" => "ID Warehouse",
            "IN" => "IN Warehouse",
            "PA" => "PA Warehouse",
            "PA2" => "PA2 Warehouse",
            "TX" => "TX Warehouse",
            _ => $"{warehouseCode} Warehouse"
        };
    }

    private static IReadOnlyDictionary<string, string> ParseIncludedWarehouseNames(JsonElement root)
    {
        if (!root.TryGetProperty("included", out var included) || included.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, string>();
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in included.EnumerateArray())
        {
            var type = FirstString(item, "type");
            if (!string.Equals(type, "warehouse", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(type, "warehouses", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = FirstString(item, "id");
            if (id is null)
            {
                continue;
            }

            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;
            names[id] = FirstString(attributes, "name", "code", "warehouse_code", "warehouseCode") ?? id;
        }

        return names;
    }

    private static string? FindItemId(JsonElement root, string sku)
    {
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        var items = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToArray()
            : data.ValueKind == JsonValueKind.Object ? [data] : [];

        foreach (var item in items)
        {
            var attributes = item.TryGetProperty("attributes", out var attributesElement) && attributesElement.ValueKind == JsonValueKind.Object
                ? attributesElement
                : item;
            var responseSku = FirstString(attributes, "sku", "part_number", "partNumber");
            if (responseSku is not null && !string.Equals(responseSku, sku, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = FirstString(item, "id", "item_id", "itemId") ?? FirstString(attributes, "id", "item_id", "itemId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return null;
    }

    private static string? FirstString(JsonElement element, params string[] propertyNames)
    {
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
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static WpsLiveInventoryResult Unavailable(string message)
    {
        return new WpsLiveInventoryResult(false, message, []);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record SkuInventoryLookup(
        string? SourceItemId,
        IReadOnlyCollection<WpsWarehouseInventoryRow> Warehouses,
        string? Message);
}
