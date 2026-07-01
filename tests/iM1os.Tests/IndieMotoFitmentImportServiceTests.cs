using System.Net;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class IndieMotoFitmentImportServiceTests
{
    [Fact]
    public async Task Import_upserts_source_and_canonical_fitment_with_throttled_request_shape()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "48EUTP",
            NormalizedManufacturerPartNumber = "48EUTP",
            Description = "Euro Style Track Pack II",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00104",
            SupplierPartNumber = "020-00104",
            ManufacturerPartNumber = "48EUTP",
            NormalizedManufacturerPartNumber = "48EUTP",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory(FitmentJson());
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("WPS", Sku: "020-00104", MaxSkus: 1, FitmentLimit: 1, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal("https://saas.indie-moto.test/api/ymm?intent=sku&sku=020-00104&limit=1", httpClientFactory.LastRequestUri);
        Assert.Equal(1, result.SkusProcessed);
        Assert.Equal(1, result.SkusWithFitment);
        Assert.Equal(0, result.SkusQueuedForPartsUnlimitedCrawl);
        Assert.Equal(0, result.SkusWithoutFitment);
        Assert.Equal(2, result.FitmentRowsProcessed);
        Assert.Equal(2, await dbContext.SupplierFitmentRecords.CountAsync());
        Assert.Equal(2, await dbContext.GlobalVehicles.CountAsync());
        Assert.Equal(2, await dbContext.VehicleFitments.CountAsync());

        var source = await dbContext.SupplierFitmentRecords.SingleAsync(x => x.Model == "FC 250");
        Assert.Equal("WPS", source.SupplierKey);
        Assert.Equal("415", source.SourceSupplierProductId);
        Assert.Equal("020-00104", source.SupplierPartNumber);
        Assert.Equal("020-00104", source.SupplierSku);
        Assert.Equal("415", source.SourceFitmentItemId);
        Assert.Equal("020-00104", source.SourceFitmentPartNumber);
        Assert.Equal("48EUTP", source.MfgPartNumber);
        Assert.Equal("offroad_dirt", source.VehicleClass);
        Assert.Equal("Offroad / Dirt", source.VehicleType);
        Assert.Equal(2018, source.Year);
        Assert.Equal("Husqvarna", source.Make);
        Assert.Equal("Resolved", source.ResolutionStatus);
        Assert.NotNull(source.GlobalVehicleId);
        Assert.NotNull(source.VehicleFitmentId);
        Assert.Equal("415", await dbContext.SupplierProducts.Select(x => x.SourceSupplierProductId).SingleAsync());
    }

    [Fact]
    public async Task Import_counts_sku_queued_for_parts_unlimited_crawler_when_fitment_is_empty()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct
        {
            Brand = "Moose",
            Description = "Brake pads",
            Status = "Active"
        };
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
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory("""
{
  "count": 0,
  "fitment": [],
  "partsUnlimitedQueue": {
    "queued": true,
    "reason": "queued",
    "partNumber": "003106"
  }
}
""");
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("PU", Sku: "003106", MaxSkus: 1, FitmentLimit: null, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal("https://saas.indie-moto.test/api/ymm?intent=sku&sku=003106", httpClientFactory.LastRequestUri);
        Assert.Equal(1, result.SkusProcessed);
        Assert.Equal(0, result.SkusWithFitment);
        Assert.Equal(1, result.SkusQueuedForPartsUnlimitedCrawl);
        Assert.Equal(0, result.SkusWithoutFitment);
        Assert.Equal(0, result.FitmentRowsProcessed);
        Assert.Empty(await dbContext.SupplierFitmentRecords.ToListAsync());
    }

    [Fact]
    public async Task Import_counts_empty_unqueued_sku_as_no_fitment()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct
        {
            Brand = "Moose",
            Description = "Brake pads",
            Status = "Active"
        };
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
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory("""
{
  "count": 0,
  "fitment": [],
  "partsUnlimitedQueue": {
    "queued": false,
    "reason": "recent_no_fitment",
    "partNumber": "003106"
  }
}
""");
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("PU", Sku: "003106", MaxSkus: 1, FitmentLimit: null, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal(1, result.SkusProcessed);
        Assert.Equal(0, result.SkusWithFitment);
        Assert.Equal(0, result.SkusQueuedForPartsUnlimitedCrawl);
        Assert.Equal(1, result.SkusWithoutFitment);
        Assert.Equal(0, result.FitmentRowsProcessed);
        Assert.Empty(await dbContext.SupplierFitmentRecords.ToListAsync());
    }

    private static string FitmentJson()
    {
        return """
{
  "ok": true,
  "scope": "global",
  "sku": "020-00104",
  "count": 2,
  "fitment": [
    {
      "supplierKey": "WPS",
      "supplierProductId": "415",
      "supplierPartNumber": "020-00104",
      "sku": "020-00104",
      "fitmentItemId": "415",
      "fitmentPartNumber": "020-00104",
      "mfgPartNumber": "48EUTP",
      "vehicleClass": "offroad_dirt",
      "vehicleClassLabel": "Offroad / Dirt",
      "year": 2018,
      "make": "Husqvarna",
      "model": "FC 250"
    },
    {
      "supplierKey": "WPS",
      "supplierProductId": "415",
      "supplierPartNumber": "020-00104",
      "sku": "020-00104",
      "fitmentItemId": "415",
      "fitmentPartNumber": "020-00104",
      "mfgPartNumber": "48EUTP",
      "vehicleClass": "offroad_dirt",
      "vehicleClassLabel": "Offroad / Dirt",
      "year": 2018,
      "make": "KTM",
      "model": "125 SX"
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

    private sealed class StaticHttpClientFactory(string responseContent) : IHttpClientFactory
    {
        private readonly StaticHttpMessageHandler handler = new(responseContent);

        public string? LastRequestUri => handler.LastRequestUri;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class StaticHttpMessageHandler(string responseContent) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            });
        }
    }
}
