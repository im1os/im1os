using System.Net;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class PartsUnlimitedLiveInventoryServiceTests
{
    [Fact]
    public async Task Live_inventory_batches_part_numbers_and_returns_warehouse_rows()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct { Brand = "Moose", Description = "Brake pads", Status = "Active" };
        var firstSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "ABC123",
            SupplierStatus = "Active"
        };
        var secondSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "XYZ456",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.AddRange(firstSupplierProduct, secondSupplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            ApiKey = "api-token",
            AuthMode = "ApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
{
  "data": [
    {
      "part_number": "ABC123",
      "warehouses": [
        { "warehouse_code": "WI", "quantity": 5 },
        { "warehouse_code": "NV", "quantity_display": "25+" }
      ]
    },
    {
      "part_number": "XYZ456",
      "inventory": {
        "east": { "quantity": 2 },
        "west": 0
      }
    }
  ]
}
""")
        });
        var service = new PartsUnlimitedLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync(
            [firstSupplierProduct.Id, secondSupplierProduct.Id],
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Null(result.Message);
        Assert.Equal("https://api.parts-unlimited.test/api/v1/parts/inventory/ABC123,XYZ456", httpClientFactory.LastRequestUri);
        Assert.Equal("api-token", httpClientFactory.LastApiKey);
        var firstResult = result.Items.Single(x => x.SupplierProductId == firstSupplierProduct.Id);
        Assert.True(firstResult.IsAvailable);
        Assert.Collection(
            firstResult.Warehouses,
            row =>
            {
                Assert.Equal("NV", row.WarehouseCode);
                Assert.Equal("25+", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("WI", row.WarehouseCode);
                Assert.Equal(5, row.Quantity);
                Assert.Equal("5", row.QuantityDisplay);
            });
        var secondResult = result.Items.Single(x => x.SupplierProductId == secondSupplierProduct.Id);
        Assert.Equal(2, secondResult.Warehouses.Count);
    }

    [Fact]
    public async Task Live_inventory_limits_batch_to_twenty_five_parts()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct { Brand = "Moose", Description = "Brake pads", Status = "Active" };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        var supplierProducts = Enumerable.Range(1, 30)
            .Select(index => new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = product.Id,
                SupplierSku = $"PART{index:00}",
                SupplierStatus = "Active"
            })
            .ToList();
        dbContext.SupplierProducts.AddRange(supplierProducts);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            ApiKey = "api-token",
            AuthMode = "ApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[]}""")
        });
        var service = new PartsUnlimitedLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync(supplierProducts.Select(x => x.Id).ToArray(), CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(25, result.Items.Count);
        Assert.NotNull(httpClientFactory.LastRequestUri);
        var partNumbers = httpClientFactory.LastRequestUri!.Split('/').Last().Split(',');
        Assert.Equal(25, partNumbers.Length);
        Assert.DoesNotContain("PART26", partNumbers);
    }

    [Fact]
    public async Task Live_inventory_parses_part_number_map_with_array_rows()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct { Brand = "Moose", Description = "Brake pads", Status = "Active" };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "003106",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            ApiKey = "api-token",
            AuthMode = "ApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
{
  "data": {
    "003106": [
      { "warehouse_code": "WI", "quantity": 3 },
      { "warehouse_code": "NV", "quantity": 0 }
    ]
  }
}
""")
        });
        var service = new PartsUnlimitedLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync([supplierProduct.Id], CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.IsAvailable);
        Assert.Collection(
            item.Warehouses,
            row =>
            {
                Assert.Equal("NV", row.WarehouseCode);
                Assert.Equal("0", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("WI", row.WarehouseCode);
                Assert.Equal("3", row.QuantityDisplay);
            });
    }

    [Fact]
    public async Task Live_inventory_parses_parts_unlimited_availability_response()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct { Brand = "Moose", Description = "Brake pads", Status = "Active" };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "003106",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            ApiKey = "api-token",
            AuthMode = "ApiKey"
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
{
  "availability": [
    {
      "partNumber": "003106",
      "warehouses": [
        { "warehouse": "WI", "quantity": "5" },
        { "warehouse": "NY", "quantity": "0" },
        { "warehouse": "TX", "quantity": "0" },
        { "warehouse": "NV", "quantity": "0" },
        { "warehouse": "NC", "quantity": "13" }
      ]
    }
  ]
}
""")
        });
        var service = new PartsUnlimitedLiveInventoryService(dbContext, httpClientFactory);

        var result = await service.GetInventoryAsync([supplierProduct.Id], CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.IsAvailable);
        Assert.Null(item.Message);
        Assert.Collection(
            item.Warehouses,
            row =>
            {
                Assert.Equal("NC", row.WarehouseCode);
                Assert.Equal(13, row.Quantity);
                Assert.Equal("13", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("NV", row.WarehouseCode);
                Assert.Equal("0", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("NY", row.WarehouseCode);
                Assert.Equal("0", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("TX", row.WarehouseCode);
                Assert.Equal("0", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("WI", row.WarehouseCode);
                Assert.Equal(5, row.Quantity);
                Assert.Equal("5", row.QuantityDisplay);
            });
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(), new TenantProvider(currentUser));
    }

    private sealed class TestClock : iM1os.Application.Common.IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingHttpClientFactory(Func<string, HttpResponseMessage> responseFactory) : IHttpClientFactory
    {
        private readonly RecordingHttpMessageHandler handler = new(responseFactory);

        public string? LastRequestUri => handler.LastRequestUri;

        public string? LastApiKey => handler.LastApiKey;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHttpMessageHandler(Func<string, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }

        public string? LastApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastApiKey = request.Headers.TryGetValues("api-key", out var values) ? values.SingleOrDefault() : null;
            return Task.FromResult(responseFactory(LastRequestUri ?? string.Empty));
        }
    }
}
