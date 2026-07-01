using System.Net;
using iM1os.Application.Common;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class WpsLiveInventoryServiceTests
{
    [Fact]
    public async Task Live_inventory_uses_sku_lookup_seeds_item_id_and_returns_warehouse_rows_without_persisting_inventory()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Description = "Euro Style Track Pack II",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00104",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            ApiKey = "token",
            AuthMode = "DataDepotDealerLoginAndApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(_ => ItemWithIncludedInventoryJson());
        var service = new WpsLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync(supplierProduct.Id, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Null(result.Message);
        Assert.Equal("https://api.wps.test/items?filter[sku]=020-00104&include=inventory", httpClientFactory.LastRequestUri);
        Assert.Equal("Bearer", httpClientFactory.LastAuthorizationScheme);
        Assert.Equal("token", httpClientFactory.LastAuthorizationParameter);
        var seededSupplierProduct = await dbContext.SupplierProducts.AsNoTracking().SingleAsync(x => x.Id == supplierProduct.Id);
        Assert.Equal("415", seededSupplierProduct.SourceSupplierProductId);
        var warehouses = result.Warehouses.ToList();
        Assert.Equal(7, warehouses.Count);
        Assert.Equal("CA", warehouses[0].WarehouseCode);
        Assert.Equal("CA Warehouse", warehouses[0].WarehouseName);
        Assert.Equal("0", warehouses[0].QuantityDisplay);
        Assert.Equal("GA", warehouses[1].WarehouseCode);
        Assert.Equal("GA Warehouse", warehouses[1].WarehouseName);
        Assert.Equal("25+", warehouses[1].QuantityDisplay);
        Assert.Equal("ID", warehouses[2].WarehouseCode);
        Assert.Equal("25+", warehouses[2].QuantityDisplay);
        Assert.Equal("IN", warehouses[3].WarehouseCode);
        Assert.Equal("25+", warehouses[3].QuantityDisplay);
        Assert.Equal("PA", warehouses[4].WarehouseCode);
        Assert.Equal("0", warehouses[4].QuantityDisplay);
        Assert.Equal("PA2", warehouses[5].WarehouseCode);
        Assert.Equal("0", warehouses[5].QuantityDisplay);
        Assert.Equal("TX", warehouses[6].WarehouseCode);
        Assert.Equal("25+", warehouses[6].QuantityDisplay);
    }

    [Fact]
    public async Task Live_inventory_falls_back_to_item_id_seeded_from_sku_lookup_when_sku_lookup_has_no_inventory()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Description = "Euro Style Track Pack II",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00104",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            ApiKey = "token",
            AuthMode = "DataDepotDealerLoginAndApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(uri =>
            uri.Contains("/items?", StringComparison.OrdinalIgnoreCase)
                ? EmptyItemJson()
                : InventoryJson());
        var service = new WpsLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync(supplierProduct.Id, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal("https://api.wps.test/inventory?filter[item_id]=415", httpClientFactory.RequestUris.Last());
        Assert.Equal(7, result.Warehouses.Count);
        var seededSupplierProduct = await dbContext.SupplierProducts.AsNoTracking().SingleAsync(x => x.Id == supplierProduct.Id);
        Assert.Equal("415", seededSupplierProduct.SourceSupplierProductId);
    }

    [Fact]
    public async Task Live_inventory_uses_seeded_item_id_without_extra_sku_lookup()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Description = "Euro Style Track Pack II",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00104",
            SourceSupplierProductId = "415",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            ApiKey = "token",
            AuthMode = "DataDepotDealerLoginAndApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(_ => InventoryJson());
        var service = new WpsLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync(supplierProduct.Id, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Single(httpClientFactory.RequestUris);
        Assert.Equal("https://api.wps.test/inventory?filter[item_id]=415", httpClientFactory.LastRequestUri);
        Assert.Equal(7, result.Warehouses.Count);
    }

    private static string ItemWithIncludedInventoryJson()
    {
        return """
{
  "data": [
    {
      "id": 415,
      "type": "items",
      "attributes": {
        "sku": "020-00104",
        "name": "Euro Style Track Pack II"
      }
    }
  ],
  "included": [
    {
      "id": 1043305,
      "type": "inventory",
      "attributes": {
        "item_id": 415,
        "sku": "020-00104",
        "ca_warehouse": 0,
        "ga_warehouse": 25,
        "id_warehouse": 25,
        "in_warehouse": 25,
        "pa_warehouse": 0,
        "pa2_warehouse": 0,
        "tx_warehouse": 25,
        "total": 100
      }
    }
  ]
}
""";
    }

    private static string EmptyItemJson()
    {
        return """
{
  "data": [
    {
      "id": 415,
      "type": "items",
      "attributes": {
        "sku": "020-00104"
      }
    }
  ],
  "included": []
}
""";
    }

    private static string InventoryJson()
    {
        return """
{
  "data": [
    {
      "id": 1043305,
      "item_id": 415,
      "sku": "020-00104",
      "ca_warehouse": 0,
      "ga_warehouse": 25,
      "id_warehouse": 25,
      "in_warehouse": 25,
      "pa_warehouse": 0,
      "pa2_warehouse": 0,
      "tx_warehouse": 25,
      "total": 100
    }
  ]
}
""";
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(), new TenantProvider(currentUser));
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingHttpClientFactory(Func<string, string> responseContent) : IHttpClientFactory
    {
        private readonly RecordingHttpMessageHandler handler = new(responseContent);

        public string? LastRequestUri => handler.LastRequestUri;

        public IReadOnlyCollection<string> RequestUris => handler.RequestUris;

        public string? LastAuthorizationScheme => handler.LastAuthorizationScheme;

        public string? LastAuthorizationParameter => handler.LastAuthorizationParameter;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHttpMessageHandler(Func<string, string> responseContent) : HttpMessageHandler
    {
        private readonly List<string> requestUris = [];

        public string? LastRequestUri { get; private set; }

        public IReadOnlyCollection<string> RequestUris => requestUris;

        public string? LastAuthorizationScheme { get; private set; }

        public string? LastAuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            if (LastRequestUri is not null)
            {
                requestUris.Add(LastRequestUri);
            }

            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastAuthorizationParameter = request.Headers.Authorization?.Parameter;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent(LastRequestUri ?? string.Empty))
            });
        }
    }
}
