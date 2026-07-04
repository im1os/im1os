using System.IO.Compression;
using System.Net;
using System.Text;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.Common;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class PartsUnlimitedBundleImportServiceTests
{
    [Fact]
    public async Task Bundle_import_is_separate_from_brand_image_import()
    {
        var now = new DateTimeOffset(2026, 6, 29, 20, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        dbContext.Suppliers.Add(supplier);
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":1}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            new PartsUnlimitedHttpClientFactory(),
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        var result = await service.ImportAsync(new PartsUnlimitedBundleImportRequest(importRun.Id, 1), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        var supplierProduct = await dbContext.SupplierProducts.SingleAsync();
        Assert.Null(supplierProduct.SupplierImagesJson);

        var brandRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBrandImages",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"ImportBrandFiles":true,"BrandFileUrls":"https://files.parts-unlimited.test/brand.zip","BrandFileMaxFiles":1}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(brandRun);
        await dbContext.SaveChangesAsync();

        var brandResult = await service.ImportAsync(new PartsUnlimitedBrandImageImportRequest(brandRun.Id, 1), CancellationToken.None);

        Assert.Equal(1, brandResult.BrandFilesProcessed);
        Assert.Equal(1, brandResult.BrandImageRowsProcessed);
        Assert.Equal(1, brandResult.BrandImagesUpdated);
        Assert.Equal(0, brandResult.BrandImageRowsUnmatched);

        supplierProduct = await dbContext.SupplierProducts.SingleAsync();
        Assert.Contains("asset.parts-unlimited.com/media/edge/6/3/6/636AE97B-7081-49B2-857C-87ADA5B0FEE2.png", supplierProduct.SupplierImagesJson);
        Assert.Contains("x=240", supplierProduct.SupplierImagesJson);

        var globalProduct = await dbContext.GlobalProducts.SingleAsync();
        Assert.Equal(supplierProduct.SupplierImagesJson, globalProduct.ImagesJson);
    }

    [Fact]
    public async Task Bundle_import_failure_includes_parts_unlimited_response_body()
    {
        var now = new DateTimeOffset(2026, 6, 30, 18, 50, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        dbContext.Suppliers.Add(supplier);
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"ImportMode":"Full","MaxItems":null}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            new StaticHttpClientFactory(HttpStatusCode.BadRequest, """{"code":"EXPORT_DAILY_LIMIT_EXCEEDED","message":"Maximum daily limit for exporting Parts Bundle has been reached."}"""),
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(new PartsUnlimitedBundleImportRequest(importRun.Id, null), CancellationToken.None));

        Assert.Contains("HTTP 400", exception.Message);
        Assert.Contains("EXPORT_DAILY_LIMIT_EXCEEDED", exception.Message);
        Assert.Contains("Maximum daily limit", exception.Message);
    }

    [Fact]
    public async Task Bundle_import_reuses_same_day_cached_bundle()
    {
        var now = new DateTimeOffset(2026, 7, 1, 20, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        dbContext.Suppliers.Add(supplier);
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = $"https://api-{Guid.NewGuid():N}.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var firstRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":1}"""
        };
        var secondRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now.AddMinutes(5),
            ParametersJson = """{"MaxItems":1}"""
        };
        dbContext.SupplierConnectorImportRuns.AddRange(firstRun, secondRun);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new CountingBundleHttpClientFactory();
        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        await service.ImportAsync(new PartsUnlimitedBundleImportRequest(firstRun.Id, 1), CancellationToken.None);
        await service.ImportAsync(new PartsUnlimitedBundleImportRequest(secondRun.Id, 1), CancellationToken.None);

        Assert.Equal(1, httpClientFactory.BundleRequestCount);
        Assert.Equal(1, await dbContext.SupplierProducts.CountAsync());
        Assert.Equal(2, await dbContext.SupplierConnectorImportRuns.CountAsync(x => x.Status == "Completed"));
    }

    [Fact]
    public async Task Brand_image_import_reuses_same_day_cached_brand_file()
    {
        var now = new DateTimeOffset(2026, 7, 2, 19, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        var globalProduct = new GlobalProduct
        {
            Brand = "MOTION PRO",
            Description = "Existing matching product",
            Status = "Active",
            IsActive = true
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = globalProduct.Id,
            SupplierSku = "20481",
            SupplierPartNumber = "20481",
            SupplierStatus = "S"
        };
        var brandFileUrl = $"https://files-{Guid.NewGuid():N}.parts-unlimited.test/brand.zip";
        var firstRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBrandImages",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = $$"""{"ImportBrandFiles":true,"BrandFileUrls":"{{brandFileUrl}}","BrandFileMaxFiles":1}"""
        };
        var secondRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBrandImages",
            Status = "Queued",
            RequestedAtUtc = now.AddMinutes(5),
            ParametersJson = $$"""{"ImportBrandFiles":true,"BrandFileUrls":"{{brandFileUrl}}","BrandFileMaxFiles":1}"""
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        dbContext.GlobalProducts.Add(globalProduct);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierConnectorImportRuns.AddRange(firstRun, secondRun);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new CountingBrandFileHttpClientFactory();
        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        var firstResult = await service.ImportAsync(new PartsUnlimitedBrandImageImportRequest(firstRun.Id, 1), CancellationToken.None);
        var secondResult = await service.ImportAsync(new PartsUnlimitedBrandImageImportRequest(secondRun.Id, 1), CancellationToken.None);

        Assert.Equal(1, httpClientFactory.BrandFileRequestCount);
        Assert.Equal(1, firstResult.BrandImagesUpdated);
        Assert.Equal(0, secondResult.BrandImagesUpdated);
        Assert.Equal(2, await dbContext.SupplierConnectorImportRuns.CountAsync(x => x.Status == "Completed"));
        Assert.Contains("asset.parts-unlimited.com/media/edge/6/3/6/636AE97B-7081-49B2-857C-87ADA5B0FEE2.png", supplierProduct.SupplierImagesJson);
    }

    [Fact]
    public async Task Bundle_import_repoints_existing_supplier_product_to_matching_global_product()
    {
        var now = new DateTimeOffset(2026, 7, 1, 21, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        var oldGlobalProduct = new GlobalProduct
        {
            Brand = "Legacy Brand",
            Manufacturer = "Legacy Brand",
            ManufacturerPartNumber = "LEGACY-20481",
            NormalizedManufacturerPartNumber = "LEGACY20481",
            Description = "Legacy product",
            Status = "Active",
            IsActive = true
        };
        var matchingGlobalProduct = new GlobalProduct
        {
            Brand = "MOTION PRO",
            Manufacturer = "MOTION PRO",
            ManufacturerPartNumber = "204801",
            NormalizedManufacturerPartNumber = "204801",
            Description = "Existing matching product",
            Status = "Active",
            IsActive = true
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(oldGlobalProduct, matchingGlobalProduct);
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = oldGlobalProduct.Id,
            SupplierSku = "20481",
            SupplierStatus = "S"
        });
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = $"https://api-{Guid.NewGuid():N}.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":1}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            new StaticZipHttpClientFactory(BundleZip()),
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        await service.ImportAsync(new PartsUnlimitedBundleImportRequest(importRun.Id, 1), CancellationToken.None);

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "20481");
        Assert.Equal(matchingGlobalProduct.Id, supplierProduct.GlobalProductId);
        oldGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == oldGlobalProduct.Id);
        Assert.Equal("Legacy Brand", oldGlobalProduct.Brand);
        Assert.Equal("LEGACY-20481", oldGlobalProduct.ManufacturerPartNumber);
    }

    [Fact]
    public async Task Bundle_import_prefers_exact_manufacturer_part_match_over_normalized_match()
    {
        var now = new DateTimeOffset(2026, 7, 1, 22, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        var normalizedGlobalProduct = new GlobalProduct
        {
            Brand = "MOTION PRO",
            Manufacturer = "MOTION PRO",
            ManufacturerPartNumber = "204-801",
            NormalizedManufacturerPartNumber = "204801",
            Description = "Existing normalized-only product",
            Status = "Active",
            IsActive = true
        };
        var exactGlobalProduct = new GlobalProduct
        {
            Brand = "MOTION PRO",
            Manufacturer = "MOTION PRO",
            ManufacturerPartNumber = "204801",
            NormalizedManufacturerPartNumber = "204801",
            Description = "Existing exact product",
            Status = "Active",
            IsActive = true
        };
        SetEntityId(normalizedGlobalProduct, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        SetEntityId(exactGlobalProduct, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(normalizedGlobalProduct, exactGlobalProduct);
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = normalizedGlobalProduct.Id,
            SupplierSku = "20481",
            SupplierStatus = "S"
        });
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = $"https://api-{Guid.NewGuid():N}.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":1}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            new StaticZipHttpClientFactory(BundleZip()),
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        await service.ImportAsync(new PartsUnlimitedBundleImportRequest(importRun.Id, 1), CancellationToken.None);

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "20481");
        Assert.Equal(exactGlobalProduct.Id, supplierProduct.GlobalProductId);
        normalizedGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == normalizedGlobalProduct.Id);
        Assert.Equal("204-801", normalizedGlobalProduct.ManufacturerPartNumber);
    }

    [Fact]
    public async Task Bundle_import_reuses_one_exact_global_product_for_duplicate_manufacturer_keys()
    {
        var now = new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        var firstOldGlobalProduct = new GlobalProduct
        {
            Brand = "Legacy AVON",
            Manufacturer = "Legacy AVON",
            ManufacturerPartNumber = "640568",
            NormalizedManufacturerPartNumber = "640568",
            Description = "First legacy product",
            Status = "Active",
            IsActive = true
        };
        var secondOldGlobalProduct = new GlobalProduct
        {
            Brand = "Old AVON",
            Manufacturer = "Old AVON",
            ManufacturerPartNumber = "640568",
            NormalizedManufacturerPartNumber = "640568",
            Description = "Second legacy product",
            Status = "Active",
            IsActive = true
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(firstOldGlobalProduct, secondOldGlobalProduct);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = firstOldGlobalProduct.Id,
                SupplierSku = "03010760",
                SupplierStatus = "S"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = secondOldGlobalProduct.Id,
                SupplierSku = "03021258",
                SupplierStatus = "S"
            });
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = $"https://api-{Guid.NewGuid():N}.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":2}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            new StaticZipHttpClientFactory(DuplicateManufacturerKeyBundleZip()),
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        await service.ImportAsync(new PartsUnlimitedBundleImportRequest(importRun.Id, 2), CancellationToken.None);

        var exactGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x =>
            x.Brand == "AVON" && x.ManufacturerPartNumber == "640568");
        var supplierProducts = await dbContext.SupplierProducts
            .Where(x => x.SupplierSku == "03010760" || x.SupplierSku == "03021258")
            .ToListAsync();
        Assert.All(supplierProducts, x => Assert.Equal(exactGlobalProduct.Id, x.GlobalProductId));
        Assert.Equal("Legacy AVON", firstOldGlobalProduct.Brand);
        Assert.Equal("Old AVON", secondOldGlobalProduct.Brand);
    }

    [Fact]
    public async Task Bundle_import_does_not_collapse_exact_keys_that_differ_by_brand_casing()
    {
        var now = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU", IsActive = true };
        var lowerCaseGlobalProduct = new GlobalProduct
        {
            Brand = "Dunlop",
            Manufacturer = "Dunlop",
            ManufacturerPartNumber = "45067287",
            NormalizedManufacturerPartNumber = "45067287",
            Description = "Existing mixed-case product",
            Status = "Active",
            IsActive = true
        };
        var exactGlobalProduct = new GlobalProduct
        {
            Brand = "DUNLOP",
            Manufacturer = "DUNLOP",
            ManufacturerPartNumber = "45067287",
            NormalizedManufacturerPartNumber = "45067287",
            Description = "Existing exact-case product",
            Status = "Active",
            IsActive = true
        };
        SetEntityId(lowerCaseGlobalProduct, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        SetEntityId(exactGlobalProduct, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(lowerCaseGlobalProduct, exactGlobalProduct);
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = $"https://api-{Guid.NewGuid():N}.parts-unlimited.test/api",
            MasterFileUrl = "/v1/parts/bundle",
            ApiKey = "pu-key",
            AuthMode = "ApiKeyHeader",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedBundle",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":1}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new PartsUnlimitedBundleImportService(
            dbContext,
            new StaticZipHttpClientFactory(DunlopCaseCollisionBundleZip()),
            new TestClock(now),
            NullLogger<PartsUnlimitedBundleImportService>.Instance);

        await service.ImportAsync(new PartsUnlimitedBundleImportRequest(importRun.Id, 1), CancellationToken.None);

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "03010601");
        Assert.Equal(exactGlobalProduct.Id, supplierProduct.GlobalProductId);
        lowerCaseGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == lowerCaseGlobalProduct.Id);
        Assert.Equal("Dunlop", lowerCaseGlobalProduct.Brand);
    }

    private static void SetEntityId(Entity entity, Guid id)
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
    }

    private static byte[] BundleZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var partInfo = archive.CreateEntry("PartInfo.csv");
            using (var writer = new StreamWriter(partInfo.Open(), Encoding.UTF8))
            {
                writer.WriteLine("Part Number,Punctuated Part Number,Vendor Part Number,Part Status,Part Description,BRAND_NAME,UPC_CODE");
                writer.WriteLine("20481,002-048-01,204801,S,STEERING CABLE SEA-DOO,MOTION PRO,123456789012");
            }

            var prices = archive.CreateEntry("D12345_PartPrices.csv");
            using var priceWriter = new StreamWriter(prices.Open(), Encoding.UTF8);
            priceWriter.WriteLine("partNumber,basePrice,dealerPrice,retailPrice,adPolicy,priceChangedToday");
            priceWriter.WriteLine("20481,63.65,63.65,79.95,Y,N");
        }

        return stream.ToArray();
    }

    private static byte[] DunlopCaseCollisionBundleZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var partInfo = archive.CreateEntry("PartInfo.csv");
            using (var writer = new StreamWriter(partInfo.Open(), Encoding.UTF8))
            {
                writer.WriteLine("Part Number,Punctuated Part Number,Vendor Part Number,Part Status,Part Description,Brand Name,UPC Code");
                writer.WriteLine("03010601,0301-0601,45067287,S,DUNLOP TIRE,DUNLOP,123456789012");
            }

            var prices = archive.CreateEntry("D12345_PartPrices.csv");
            using var priceWriter = new StreamWriter(prices.Open(), Encoding.UTF8);
            priceWriter.WriteLine("partNumber,basePrice,dealerPrice,retailPrice,adPolicy,priceChangedToday");
            priceWriter.WriteLine("03010601,63.65,63.65,79.95,Y,N");
        }

        return stream.ToArray();
    }

    private static byte[] DuplicateManufacturerKeyBundleZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var partInfo = archive.CreateEntry("PartInfo.csv");
            using (var writer = new StreamWriter(partInfo.Open(), Encoding.UTF8))
            {
                writer.WriteLine("Part Number,Punctuated Part Number,Vendor Part Number,Part Status,Part Description,Brand Name,UPC Code");
                writer.WriteLine("03010760,0301-0760,640568,S,AVON TIRE FIRST,AVON,123456789012");
                writer.WriteLine("03021258,0302-1258,640568,S,AVON TIRE SECOND,AVON,123456789013");
            }

            var prices = archive.CreateEntry("D12345_PartPrices.csv");
            using var priceWriter = new StreamWriter(prices.Open(), Encoding.UTF8);
            priceWriter.WriteLine("partNumber,basePrice,dealerPrice,retailPrice,adPolicy,priceChangedToday");
            priceWriter.WriteLine("03010760,63.65,63.65,79.95,Y,N");
            priceWriter.WriteLine("03021258,63.65,63.65,79.95,Y,N");
        }

        return stream.ToArray();
    }

    private static byte[] BrandZip()
    {
        var encodedImage = Convert.ToBase64String(Encoding.UTF8.GetBytes("/6/3/6/636AE97B-7081-49B2-857C-87ADA5B0FEE2"));
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("brand.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"""
<items>
  <part>
    <partNumber>20481</partNumber>
    <partImage>https://brand.parts.test/images/{encodedImage}</partImage>
  </part>
</items>
""");
        }

        return stream.ToArray();
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

    private sealed class PartsUnlimitedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new PartsUnlimitedHttpMessageHandler());
        }
    }

    private sealed class PartsUnlimitedHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.RequestUri?.Host.Contains("files.parts-unlimited.test", StringComparison.OrdinalIgnoreCase) == true
                ? BrandZip()
                : BundleZip();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }

    private sealed class StaticHttpClientFactory(HttpStatusCode statusCode, string body) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHttpMessageHandler(statusCode, body));
        }
    }

    private sealed class StaticHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingBundleHttpClientFactory : IHttpClientFactory
    {
        private readonly CountingBundleHttpMessageHandler handler = new();

        public int BundleRequestCount => handler.BundleRequestCount;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class CountingBundleHttpMessageHandler : HttpMessageHandler
    {
        public int BundleRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            BundleRequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(BundleZip())
            });
        }
    }

    private sealed class CountingBrandFileHttpClientFactory : IHttpClientFactory
    {
        private readonly CountingBrandFileHttpMessageHandler handler = new();

        public int BrandFileRequestCount => handler.BrandFileRequestCount;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class CountingBrandFileHttpMessageHandler : HttpMessageHandler
    {
        public int BrandFileRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            BrandFileRequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(BrandZip())
            });
        }
    }

    private sealed class StaticZipHttpClientFactory(byte[] content) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticZipHttpMessageHandler(content));
        }
    }

    private sealed class StaticZipHttpMessageHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}
