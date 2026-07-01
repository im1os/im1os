using System.Net;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class WpsMasterItemListImportServiceTests
{
    [Fact]
    public async Task Import_processes_master_items_up_to_configured_limit()
    {
        var now = new DateTimeOffset(2026, 6, 29, 18, 45, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var httpClientFactory = new StaticHttpClientFactory(MasterItemJson());
        var service = new WpsMasterItemListImportService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<WpsMasterItemListImportService>.Instance);
        var importRunId = await WpsMasterItemListImportService.EnsureWpsImportRunAsync(dbContext, new TestClock(now), 2, CancellationToken.None);

        var result = await service.ImportAsync(new WpsMasterItemListImportRequest(importRunId, 2), CancellationToken.None);

        Assert.Equal(2, result.Processed);
        Assert.Equal(2, await dbContext.SupplierProducts.CountAsync());
        Assert.Equal(2, await dbContext.GlobalProducts.CountAsync());
        Assert.Equal(2, await dbContext.SupplierPrices.CountAsync());

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "020-00010");
        Assert.Equal("TC-M6M8", supplierProduct.ManufacturerPartNumber);
        Assert.Equal("TCM6M8", supplierProduct.NormalizedManufacturerPartNumber);
        Assert.Contains("drop_ship_fee", supplierProduct.SourceDataJson);
        Assert.Contains("unmapped_future_field", supplierProduct.SourceDataJson);

        var price = await dbContext.SupplierPrices.SingleAsync(x => x.SupplierProductId == supplierProduct.Id);
        Assert.Equal(4.99m, price.Msrp);
        Assert.Equal(0.00m, price.Map);
        Assert.Equal(0m, price.DealerCost);

        var run = await dbContext.SupplierConnectorImportRuns.SingleAsync(x => x.Id == importRunId);
        Assert.Equal("Completed", run.Status);
        Assert.Contains("Processed 2 items", run.Message);
    }

    [Fact]
    public async Task Import_reuses_tracked_global_product_for_duplicate_vendor_numbers_in_same_batch()
    {
        var now = new DateTimeOffset(2026, 6, 30, 19, 20, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var httpClientFactory = new StaticHttpClientFactory("""
[
  {
    "sku": "100-00001",
    "name": "DUPLICATE PART A",
    "list_price": 10.00,
    "brand": "BOLT",
    "vendor_number": "DUP-100",
    "status": "STK"
  },
  {
    "sku": "100-00002",
    "name": "DUPLICATE PART B",
    "list_price": 11.00,
    "brand": "BOLT",
    "vendor_number": "DUP-100",
    "status": "STK"
  }
]
""");
        var service = new WpsMasterItemListImportService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<WpsMasterItemListImportService>.Instance);
        var importRunId = await WpsMasterItemListImportService.EnsureWpsImportRunAsync(dbContext, new TestClock(now), null, CancellationToken.None);

        var result = await service.ImportAsync(new WpsMasterItemListImportRequest(importRunId, null), CancellationToken.None);

        Assert.Equal(2, result.Processed);
        Assert.Equal(2, await dbContext.SupplierProducts.CountAsync());
        Assert.Equal(1, await dbContext.GlobalProducts.CountAsync());
        Assert.All(await dbContext.SupplierProducts.ToListAsync(), x => Assert.Equal("DUP-100", x.ManufacturerPartNumber));
    }

    private static string MasterItemJson()
    {
        return """
[
  {
    "sku": "020-00010",
    "name": "M6/M8 THREAD CHASERS",
    "list_price": 4.99,
    "standard_dealer_price": 3.26,
    "brand": "BOLT",
    "vendor_number": "TC-M6M8",
    "status": "STK",
    "upc": "819648023063",
    "length": 2.75,
    "width": 4.12,
    "height": 0.25,
    "weight": 0.08,
    "has_map_policy": "true",
    "mapp_price": "0.00",
    "drop_ship_eligible": "true",
    "drop_ship_fee": "FR",
    "product_name": "Thread Chasers",
    "product_type": "Hardware/Fasteners/Fittings",
    "product_description": "Thread Chasers are designed to clean threads.",
    "product_features": "<ul><li>Thread chasers</li></ul>",
    "primary_item_image": "http://cdn.wpsstatic.com/images/full/thread-chaser.jpg",
    "unmapped_future_field": "kept"
  },
  {
    "sku": "020-00100",
    "name": "OFF-ROAD METRIC BOLT KIT",
    "list_price": 55.99,
    "standard_dealer_price": 33.60,
    "brand": "BOLT",
    "vendor_number": "2004-PP",
    "status": "STK",
    "upc": "819648020086",
    "mapp_price": "0.00",
    "product_name": "Japanese Style Metric Pro-Pack Kit",
    "product_type": "Hardware/Fasteners/Fittings"
  },
  {
    "sku": "020-00101",
    "name": "SHOULD NOT IMPORT",
    "list_price": 16.99,
    "standard_dealer_price": 10.00,
    "brand": "BOLT",
    "vendor_number": "54TRKPK",
    "status": "STK"
  }
]
""";
    }

    private static ApplicationDbContext CreateContext(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(now), new TenantProvider(currentUser));
    }

    private sealed class TestClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class StaticHttpClientFactory(string responseContent) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHttpMessageHandler(responseContent));
        }
    }

    private sealed class StaticHttpMessageHandler(string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });
        }
    }
}
