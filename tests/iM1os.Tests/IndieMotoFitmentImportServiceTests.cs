using System.Net;
using System.Text.Json;
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
    public async Task Import_upserts_source_and_canonical_fitment_with_streaming_export_request_shape()
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

        var httpClientFactory = new StaticHttpClientFactory(SingleWpsFitmentExportNdjson());
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("WPS", Sku: "020-00104", MaxSkus: 1, FitmentLimit: 1, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, httpClientFactory.LastMethod);
        Assert.Equal("https://saas.indie-moto.test/api/ymm", httpClientFactory.LastRequestUri);
        using (var body = JsonDocument.Parse(httpClientFactory.LastRequestBody!))
        {
            Assert.Equal("fitment-export", body.RootElement.GetProperty("intent").GetString());
            Assert.Equal("WPS", body.RootElement.GetProperty("supplier").GetString());
            Assert.True(body.RootElement.GetProperty("includeMisses").GetBoolean());
            Assert.True(body.RootElement.GetProperty("queuePartsUnlimitedMisses").GetBoolean());
            Assert.Equal(["020-00104"], body.RootElement.GetProperty("skus").EnumerateArray().Select(x => x.GetString()!).ToArray());
        }
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
    public async Task Import_uses_streaming_fitment_export_for_wps_multiple_skus()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var firstProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "48EUTP",
            NormalizedManufacturerPartNumber = "48EUTP",
            Description = "Euro Style Track Pack II",
            Status = "Active"
        };
        var secondProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "ABC123",
            NormalizedManufacturerPartNumber = "ABC123",
            Description = "Empty fitment product",
            Status = "Active"
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(firstProduct, secondProduct);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = firstProduct.Id,
                SupplierSku = "020-00104",
                SupplierPartNumber = "020-00104",
                SupplierStatus = "STK"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = secondProduct.Id,
                SupplierSku = "020-00105",
                SupplierPartNumber = "020-00105",
                SupplierStatus = "STK"
            });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory(WpsFitmentExportNdjson());
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("WPS", Sku: null, MaxSkus: 2, FitmentLimit: 1000, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, httpClientFactory.LastMethod);
        Assert.Equal("https://saas.indie-moto.test/api/ymm", httpClientFactory.LastRequestUri);
        using var body = JsonDocument.Parse(httpClientFactory.LastRequestBody!);
        Assert.Equal("fitment-export", body.RootElement.GetProperty("intent").GetString());
        Assert.Equal("WPS", body.RootElement.GetProperty("supplier").GetString());
        Assert.True(body.RootElement.GetProperty("includeMisses").GetBoolean());
        Assert.True(body.RootElement.GetProperty("queuePartsUnlimitedMisses").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("limit", out _));
        Assert.Equal(["020-00104", "020-00105"], body.RootElement.GetProperty("skus").EnumerateArray().Select(x => x.GetString()!).ToArray());

        Assert.Equal(2, result.SkusProcessed);
        Assert.Equal(1, result.SkusWithFitment);
        Assert.Equal(1, result.SkusQueuedForPartsUnlimitedCrawl);
        Assert.Equal(0, result.SkusWithoutFitment);
        Assert.Equal(1, result.FitmentRowsProcessed);
        Assert.Equal(1, await dbContext.SupplierFitmentRecords.CountAsync());
        Assert.Equal(1, await dbContext.GlobalVehicles.CountAsync());
        Assert.Equal(1, await dbContext.VehicleFitments.CountAsync());
    }

    [Fact]
    public async Task Import_sends_turn14_supplier_detail_for_streaming_export_with_parts_unlimited_queue_flag()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Turn14", Code = "TURN14", ConnectorKey = "TURN14" };
        var firstProduct = new GlobalProduct
        {
            Brand = "Acerbis",
            Description = "First product",
            Status = "Active"
        };
        var secondProduct = new GlobalProduct
        {
            Brand = "Acerbis",
            Description = "Second product",
            Status = "Active"
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(firstProduct, secondProduct);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = firstProduct.Id,
                SupplierSku = "acb2645481007",
                SupplierPartNumber = "acb2645481007",
                ManufacturerPartNumber = "2645481007",
                SupplierStatus = "Active"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = secondProduct.Id,
                SupplierSku = "acb2645481008",
                SupplierPartNumber = "acb2645481008",
                ManufacturerPartNumber = "2645481008",
                SupplierStatus = "Active"
            });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory("""
{"type":"miss","requestedSku":"acb2645481007","supplierKey":"Turn14","partsUnlimitedQueue":null}
{"type":"miss","requestedSku":"acb2645481008","supplierKey":"Turn14","partsUnlimitedQueue":null}
""");
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("TURN14", Sku: null, MaxSkus: 2, FitmentLimit: 1000, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        using var body = JsonDocument.Parse(httpClientFactory.LastRequestBody!);
        Assert.Equal("fitment-export", body.RootElement.GetProperty("intent").GetString());
        Assert.Equal("Turn14", body.RootElement.GetProperty("supplier").GetString());
        Assert.True(body.RootElement.GetProperty("includeMisses").GetBoolean());
        Assert.True(body.RootElement.GetProperty("queuePartsUnlimitedMisses").GetBoolean());
        Assert.Equal(["acb2645481007", "acb2645481008"], body.RootElement.GetProperty("skus").EnumerateArray().Select(x => x.GetString()!).ToArray());

        Assert.Equal(2, result.SkusProcessed);
        Assert.Equal(0, result.SkusQueuedForPartsUnlimitedCrawl);
        Assert.Equal(2, result.SkusWithoutFitment);
    }

    [Fact]
    public async Task Import_sends_parts_unlimited_streaming_export_without_queue_flag()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var firstProduct = new GlobalProduct
        {
            Brand = "Moose",
            Description = "First product",
            Status = "Active"
        };
        var secondProduct = new GlobalProduct
        {
            Brand = "Moose",
            Description = "Second product",
            Status = "Active"
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(firstProduct, secondProduct);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = firstProduct.Id,
                SupplierSku = "003106",
                SupplierPartNumber = "003106",
                SupplierStatus = "Active"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = secondProduct.Id,
                SupplierSku = "003107",
                SupplierPartNumber = "003107",
                SupplierStatus = "Active"
            });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory("""
{"type":"miss","requestedSku":"003106","supplierKey":"Parts Unlimited","partsUnlimitedQueue":null}
{"type":"miss","requestedSku":"003107","supplierKey":"Parts Unlimited","partsUnlimitedQueue":null}
""");
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("PU", Sku: null, MaxSkus: 2, FitmentLimit: 1000, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        using var body = JsonDocument.Parse(httpClientFactory.LastRequestBody!);
        Assert.Equal("fitment-export", body.RootElement.GetProperty("intent").GetString());
        Assert.Equal("Parts Unlimited", body.RootElement.GetProperty("supplier").GetString());
        Assert.True(body.RootElement.GetProperty("includeMisses").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("queuePartsUnlimitedMisses", out _));
        Assert.Equal(["003106", "003107"], body.RootElement.GetProperty("skus").EnumerateArray().Select(x => x.GetString()!).ToArray());

        Assert.Equal(2, result.SkusProcessed);
        Assert.Equal(0, result.SkusWithFitment);
        Assert.Equal(0, result.SkusQueuedForPartsUnlimitedCrawl);
        Assert.Equal(2, result.SkusWithoutFitment);
        Assert.Equal(0, result.FailedSkus);
    }

    [Fact]
    public async Task Import_falls_back_to_single_sku_lookup_when_batch_action_is_not_live()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var firstProduct = new GlobalProduct
        {
            Brand = "WSM",
            Description = "First product",
            Status = "Active"
        };
        var secondProduct = new GlobalProduct
        {
            Brand = "WSM",
            Description = "Second product",
            Status = "Active"
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(firstProduct, secondProduct);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = firstProduct.Id,
                SupplierSku = "003106",
                SupplierStatus = "Active"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = secondProduct.Id,
                SupplierSku = "003107",
                SupplierStatus = "Active"
            });
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new BatchUnsupportedHttpClientFactory();
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("PU", Sku: null, MaxSkus: 2, FitmentLimit: 1000, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal(2, httpClientFactory.PostRequestCount);
        Assert.Equal(2, httpClientFactory.GetRequestCount);
        Assert.Equal(2, result.SkusProcessed);
        Assert.Equal(2, result.SkusWithoutFitment);
        Assert.Equal(0, result.FailedSkus);
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
{"type":"miss","requestedSku":"003106","supplierKey":"Parts Unlimited","partsUnlimitedQueue":{"queued":true,"reason":"queued","partNumber":"003106"}}
""");
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("PU", Sku: "003106", MaxSkus: 1, FitmentLimit: null, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal("https://saas.indie-moto.test/api/ymm", httpClientFactory.LastRequestUri);
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
{"type":"miss","requestedSku":"003106","supplierKey":"Parts Unlimited","partsUnlimitedQueue":{"queued":false,"reason":"recent_no_fitment","partNumber":"003106"}}
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

    [Fact]
    public async Task Import_rejects_fitment_rows_for_the_wrong_supplier_or_sku()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var product = new GlobalProduct
        {
            Brand = "Twin Air",
            Manufacturer = "Twin Air",
            ManufacturerPartNumber = "159010",
            NormalizedManufacturerPartNumber = "159010",
            Description = "Oiling Tub",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "22999",
            SupplierPartNumber = "22999",
            ManufacturerPartNumber = "159010",
            NormalizedManufacturerPartNumber = "159010",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new StaticHttpClientFactory("""
{"requestedSku":"22999","supplierKey":"WPS","supplierProductId":"22999","supplierPartNumber":"2-BR9ECMIX","sku":"2-BR9ECMIX","fitmentItemId":"22999","fitmentPartNumber":"2-BR9ECMIX","mfgPartNumber":"2707","vehicleClass":"offroad_dirt","vehicleClassLabel":"Offroad / Dirt","year":2018,"make":"KTM","model":"125 SX"}
""");
        var service = new IndieMotoFitmentImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<IndieMotoFitmentImportService>.Instance);

        var result = await service.ImportAsync(
            new IndieMotoFitmentImportRequest("PU", Sku: "22999", MaxSkus: 1, FitmentLimit: null, DelayMilliseconds: 0, BaseUrl: "https://saas.indie-moto.test"),
            CancellationToken.None);

        Assert.Equal(1, result.SkusProcessed);
        Assert.Equal(0, result.SkusWithFitment);
        Assert.Equal(1, result.SkusWithoutFitment);
        Assert.Equal(0, result.FitmentRowsProcessed);
        Assert.Empty(await dbContext.SupplierFitmentRecords.ToListAsync());
        Assert.Empty(await dbContext.VehicleFitments.ToListAsync());
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

    private static string SingleWpsFitmentExportNdjson()
    {
        return """
{"requestedSku":"020-00104","supplierKey":"WPS","supplierProductId":"415","supplierPartNumber":"020-00104","sku":"020-00104","fitmentItemId":"415","fitmentPartNumber":"020-00104","mfgPartNumber":"48EUTP","vehicleClass":"offroad_dirt","vehicleClassLabel":"Offroad / Dirt","year":2018,"make":"Husqvarna","model":"FC 250"}
{"requestedSku":"020-00104","supplierKey":"WPS","supplierProductId":"415","supplierPartNumber":"020-00104","sku":"020-00104","fitmentItemId":"415","fitmentPartNumber":"020-00104","mfgPartNumber":"48EUTP","vehicleClass":"offroad_dirt","vehicleClassLabel":"Offroad / Dirt","year":2018,"make":"KTM","model":"125 SX"}
""";
    }

    private static string BatchFitmentJson()
    {
        return """
{
  "ok": true,
  "requestedCount": 2,
  "matchedCount": 1,
  "fitmentCount": 1,
  "maxBatchSize": 500,
  "items": [
    {
      "sku": "020-00104",
      "count": 1,
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
        }
      ]
    },
    {
      "sku": "020-00105",
      "count": 0,
      "fitment": []
    }
  ],
  "results": {
    "020-00104": [
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
      }
    ],
    "020-00105": []
  },
  "partsUnlimitedQueue": {
    "attemptedCount": 1,
    "queuedCount": 1,
    "skippedCount": 0,
    "results": [
      {
        "queued": true,
        "reason": "queued",
        "partNumber": "020-00105",
        "sourceId": "fitment_source_02000105",
        "storedCount": 0,
        "lastRefreshedAt": null
      }
    ]
  }
}
""";
    }

    private static string WpsFitmentExportNdjson()
    {
        return """
{"requestedSku":"020-00104","supplierKey":"WPS","supplierProductId":"415","supplierPartNumber":"020-00104","year":2018,"make":"Husqvarna","model":"FC 250","vehicleClass":"offroad_dirt","vehicleClassLabel":"Offroad / Dirt","mfgPartNumber":"48EUTP"}
{"type":"miss","requestedSku":"020-00105","supplierKey":"WPS","partsUnlimitedQueue":{"queued":true,"reason":"queued"}}
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
        public HttpMethod? LastMethod => handler.LastMethod;
        public string? LastRequestBody => handler.LastRequestBody;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class StaticHttpMessageHandler(string responseContent) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastMethod = request.Method;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            };
        }
    }

    private sealed class BatchUnsupportedHttpClientFactory : IHttpClientFactory
    {
        private readonly BatchUnsupportedHttpMessageHandler handler = new();

        public int PostRequestCount => handler.PostRequestCount;
        public int GetRequestCount => handler.GetRequestCount;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class BatchUnsupportedHttpMessageHandler : HttpMessageHandler
    {
        public int PostRequestCount { get; private set; }
        public int GetRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                PostRequestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"ok":false,"error":"Unknown YMM API action."}""")
                });
            }

            GetRequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"fitment":[]}""")
            });
        }
    }
}
