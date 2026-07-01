using System.Net;
using iM1os.Application.Common;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class Turn14LiveInventoryServiceTests
{
    [Fact]
    public async Task Live_inventory_falls_back_to_cached_warehouse_availability_when_item_id_is_not_found()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var product = new GlobalProduct { Brand = "RAD", Description = "Clutch Fork Stop", Status = "Active" };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "rad20-0262",
            SourceSupplierProductId = "bad-id",
            SupplierStatus = "Active",
            WarehouseAvailability = """[{"location_id":"01","can_place_order":true},{"location_id":"02","can_place_order":false}]"""
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "TURN14",
            DisplayName = "Turn14",
            AuthMode = "API",
            ApiKey = "platform-client-id",
            ApiSecretProtected = """{"apiClientSecret":"client-secret"}""",
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(uri => uri.EndsWith("/token", StringComparison.OrdinalIgnoreCase)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token","expires_in":3600}""")
            }
            : new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"errors":{"status":"404","title":"not found"}}""")
            });
        var service = new Turn14LiveInventoryService(dbContext, httpClientFactory, new TestClock());

        var result = await service.GetInventoryAsync(supplierProduct.Id, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Contains("client_id=platform-client-id", httpClientFactory.LastTokenRequestBody);
        Assert.Contains("client_secret=client-secret", httpClientFactory.LastTokenRequestBody);
        Assert.Contains("last imported warehouse availability", result.Message);
        Assert.Collection(
            result.Warehouses,
            row =>
            {
                Assert.Equal("01", row.WarehouseCode);
                Assert.Equal("Available", row.QuantityDisplay);
            },
            row =>
            {
                Assert.Equal("02", row.WarehouseCode);
                Assert.Equal("Unavailable", row.QuantityDisplay);
            });
    }

    [Fact]
    public async Task Live_inventory_treats_not_found_without_cached_availability_as_no_inventory()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var product = new GlobalProduct { Brand = "RAD", Description = "Clutch Fork Stop", Status = "Active" };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "rad20-0262",
            SourceSupplierProductId = "bad-id",
            SupplierStatus = "Active"
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorConfigurations.Add(new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "TURN14",
            DisplayName = "Turn14",
            AuthMode = "API",
            ApiKey = "client-id",
            ApiSecretProtected = """{"apiClientSecret":"client-secret"}""",
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory(uri => uri.EndsWith("/token", StringComparison.OrdinalIgnoreCase)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token","expires_in":3600}""")
            }
            : new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"errors":{"status":"404","title":"not found"}}""")
            });
        var service = new Turn14LiveInventoryService(dbContext, httpClientFactory, new TestClock());

        var result = await service.GetInventoryAsync(supplierProduct.Id, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal("No Turn14 warehouse inventory was returned.", result.Message);
        Assert.Empty(result.Warehouses);
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

    private sealed class RecordingHttpClientFactory(Func<string, HttpResponseMessage> responseFactory) : IHttpClientFactory
    {
        private readonly RecordingHttpMessageHandler handler = new(responseFactory);

        public string? LastTokenRequestBody => handler.LastTokenRequestBody;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHttpMessageHandler(Func<string, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public string? LastTokenRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            if (uri.EndsWith("/token", StringComparison.OrdinalIgnoreCase) && request.Content is not null)
            {
                LastTokenRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return responseFactory(uri);
        }
    }
}
