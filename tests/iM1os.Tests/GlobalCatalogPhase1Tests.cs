using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class GlobalCatalogPhase1Tests
{
    [Fact]
    public async Task Product_matching_uses_existing_supplier_sku_mapping_first()
    {
        var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "NGK",
            Manufacturer = "NGK",
            ManufacturerPartNumber = "BR8ES",
            Description = "Spark plug",
            Upc = "087295123456",
            Status = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "WPS-12345",
            SupplierPartNumber = "BR8ES",
            SupplierStatus = "Active"
        });
        await dbContext.SaveChangesAsync();

        var service = new ProductMatchingService(dbContext);

        var result = await service.MatchAsync(new ProductMatchRequest(
            supplier.Id,
            "WPS-12345",
            Upc: "DIFFERENT",
            ManufacturerPartNumber: "DIFFERENT",
            Brand: "Other",
            SupplierDescription: "Supplier description"),
            CancellationToken.None);

        Assert.Equal(ProductMatchType.SupplierSkuMapping, result.MatchType);
        Assert.Equal(product.Id, result.GlobalProductId);
        Assert.False(result.RequiresManualReview);
        Assert.Empty(await dbContext.ProductMatchReviewItems.ToListAsync());
    }

    [Fact]
    public async Task Product_matching_uses_upc_when_supplier_mapping_does_not_exist()
    {
        var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU" };
        var product = new GlobalProduct
        {
            Brand = "Twin Air",
            Manufacturer = "Twin Air",
            ManufacturerPartNumber = "TA-150220",
            Description = "Air filter",
            Upc = "123456789012",
            Status = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new ProductMatchingService(dbContext);

        var result = await service.MatchAsync(new ProductMatchRequest(
            supplier.Id,
            "PU-98765",
            Upc: "123456789012",
            ManufacturerPartNumber: "NOT-A-MATCH",
            Brand: "Twin Air",
            SupplierDescription: "Air filter"),
            CancellationToken.None);

        Assert.Equal(ProductMatchType.Upc, result.MatchType);
        Assert.Equal(product.Id, result.GlobalProductId);
        Assert.Equal(0.98m, result.Confidence);
        Assert.False(result.RequiresManualReview);
    }

    [Fact]
    public async Task Product_matching_uses_brand_and_part_number_to_disambiguate()
    {
        var dbContext = CreateContext();
        var supplier = new Supplier { Name = "OEM KTM", Code = "KTM" };
        var ktmProduct = new GlobalProduct
        {
            Brand = "KTM",
            Manufacturer = "KTM",
            ManufacturerPartNumber = "79013001044",
            Description = "Fork seal kit",
            Status = "Active"
        };
        var aftermarketProduct = new GlobalProduct
        {
            Brand = "Aftermarket",
            Manufacturer = "Aftermarket",
            ManufacturerPartNumber = "79013001044",
            Description = "Fork seal kit alternate",
            Status = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(ktmProduct, aftermarketProduct);
        await dbContext.SaveChangesAsync();

        var service = new ProductMatchingService(dbContext);

        var result = await service.MatchAsync(new ProductMatchRequest(
            supplier.Id,
            "KTM-79013001044",
            Upc: null,
            ManufacturerPartNumber: "79013001044",
            Brand: "KTM",
            SupplierDescription: "Fork seal kit"),
            CancellationToken.None);

        Assert.Equal(ProductMatchType.BrandAndPartNumber, result.MatchType);
        Assert.Equal(ktmProduct.Id, result.GlobalProductId);
        Assert.False(result.RequiresManualReview);
    }

    [Fact]
    public async Task Product_matching_uses_normalized_manufacturer_part_number_across_suppliers()
    {
        var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "NGK",
            Manufacturer = "NGK",
            ManufacturerPartNumber = "BR8ES",
            NormalizedManufacturerPartNumber = "BR8ES",
            Description = "Spark plug",
            Status = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new ProductMatchingService(dbContext);

        var result = await service.MatchAsync(new ProductMatchRequest(
            supplier.Id,
            "WPS-PLUG-1",
            Upc: null,
            ManufacturerPartNumber: "BR-8 ES",
            Brand: "NGK",
            SupplierDescription: "Spark plug"),
            CancellationToken.None);

        Assert.Equal(ProductMatchType.ManufacturerPartNumber, result.MatchType);
        Assert.Equal(product.Id, result.GlobalProductId);
        Assert.False(result.RequiresManualReview);
    }

    [Fact]
    public async Task Supplier_product_preserves_raw_supplier_payload_for_unmapped_fields()
    {
        var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            Description = "M6/M8 thread chasers",
            Status = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00010",
            SupplierPartNumber = "020-00010",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            SupplierDescription = "M6/M8 thread chasers",
            SupplierStatus = "STK",
            SourceDataJson = """{"sku":"020-00010","vendor_number":"TC-M6M8","drop_ship_fee":"FR","unmapped_future_field":"kept"}"""
        });
        await dbContext.SaveChangesAsync();

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync();

        Assert.Contains("unmapped_future_field", supplierProduct.SourceDataJson);
        Assert.Contains("drop_ship_fee", supplierProduct.SourceDataJson);
    }

    [Theory]
    [InlineData("0", null)]
    [InlineData("000000000000", null)]
    [InlineData("N/A", null)]
    [InlineData("123456789012", "123456789012")]
    [InlineData("12345-67890-12", "123456789012")]
    public void Catalog_normalization_rejects_placeholder_upc_values(string value, string? expected)
    {
        var method = typeof(CatalogNormalizationService).GetMethod(
            "CleanUpc",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(expected, method.Invoke(null, [value]));
    }

    [Fact]
    public async Task Catalog_normalization_refreshes_stale_brand_alias_source_links()
    {
        var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS" };
        var pu = new Supplier { Name = "Parts Unlimited", Code = "PU" };
        var wpsProduct = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "78920",
            NormalizedManufacturerPartNumber = "78920",
            Description = "SC1 Silicone Spray - 78920",
            Upc = "51027013030",
            Status = "Active"
        };
        var puProduct = new GlobalProduct
        {
            Brand = "MAXIMA RACING OIL",
            Manufacturer = "MAXIMA RACING OIL",
            ManufacturerPartNumber = "78920",
            NormalizedManufacturerPartNumber = "78920",
            Description = "DETAILER SC1 AERO 13.2OZ - 78920",
            Upc = "051027013030",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "78-9944",
            SupplierPartNumber = "78-9944",
            ManufacturerPartNumber = "78920",
            NormalizedManufacturerPartNumber = "78920",
            SupplierDescription = "HIGH GLOSS SC1 CLEAR COAT SILICONE SPRAY 18.9OZ",
            SupplierStatus = "STK"
        };
        var puSupplierProduct = new SupplierProduct
        {
            SupplierId = pu.Id,
            GlobalProductId = puProduct.Id,
            SupplierSku = "37060133",
            SupplierPartNumber = "3706-0133",
            ManufacturerPartNumber = "78920",
            NormalizedManufacturerPartNumber = "78920",
            SupplierDescription = "DETAILER SC1 AERO 13.2OZ",
            SupplierStatus = "Active"
        };
        var maximaCanonical = new CanonicalItem
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "78920",
            NormalizedManufacturerPartNumber = "78920",
            Title = "SC1 Silicone Spray - 78920",
            PrimaryUpc = "51027013030",
            Status = "Active"
        };
        var stalePuCanonical = new CanonicalItem
        {
            Brand = "MAXIMA RACING OIL",
            Manufacturer = "MAXIMA RACING OIL",
            ManufacturerPartNumber = "78920",
            NormalizedManufacturerPartNumber = "78920",
            Title = "DETAILER SC1 AERO 13.2OZ - 78920",
            PrimaryUpc = "051027013030",
            Status = "Active"
        };
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = pu.Id,
            ConnectorKey = "CatalogNormalization",
            DisplayName = "Catalog normalization",
            AuthMode = "None",
            IsEnabled = true
        };
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "CatalogNormalization",
            Status = "Running",
            RequestedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
        };

        dbContext.Suppliers.AddRange(wps, pu);
        dbContext.GlobalProducts.AddRange(wpsProduct, puProduct);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, puSupplierProduct);
        dbContext.CanonicalItems.AddRange(maximaCanonical, stalePuCanonical);
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        dbContext.CanonicalItemSources.AddRange(
            new CanonicalItemSource
            {
                CanonicalItemId = maximaCanonical.Id,
                GlobalProductId = wpsProduct.Id,
                SupplierId = wps.Id,
                SupplierProductId = wpsSupplierProduct.Id,
                SupplierCode = "WPS",
                SourceTable = "supplier_products",
                SourceKey = wpsSupplierProduct.Id.ToString("N"),
                MatchMethod = "cross_supplier_brand_raw_mfg_part"
            },
            new CanonicalItemSource
            {
                CanonicalItemId = stalePuCanonical.Id,
                GlobalProductId = puProduct.Id,
                SupplierId = pu.Id,
                SupplierProductId = puSupplierProduct.Id,
                SupplierCode = "PU",
                SourceTable = "supplier_products",
                SourceKey = puSupplierProduct.Id.ToString("N"),
                MatchMethod = "existing_source_link"
            });
        dbContext.CanonicalItemSupplierOffers.AddRange(
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = maximaCanonical.Id,
                SupplierId = wps.Id,
                SupplierProductId = wpsSupplierProduct.Id,
                SupplierCode = "WPS",
                SupplierSku = "78-9944",
                SupplierPartNumber = "78-9944",
                SupplierTitle = "HIGH GLOSS SC1 CLEAR COAT SILICONE SPRAY 18.9OZ",
                Status = "STK"
            },
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = stalePuCanonical.Id,
                SupplierId = pu.Id,
                SupplierProductId = puSupplierProduct.Id,
                SupplierCode = "PU",
                SupplierSku = "37060133",
                SupplierPartNumber = "3706-0133",
                SupplierTitle = "DETAILER SC1 AERO 13.2OZ",
                Status = "Active"
            });
        await dbContext.SaveChangesAsync();

        var service = new CatalogNormalizationService(dbContext);

        await service.NormalizeAsync(new CatalogNormalizationRequest(importRun.Id, null), CancellationToken.None);

        var puSource = await dbContext.CanonicalItemSources.SingleAsync(x => x.SupplierProductId == puSupplierProduct.Id);
        var puOffer = await dbContext.CanonicalItemSupplierOffers.SingleAsync(x => x.SupplierProductId == puSupplierProduct.Id);
        var reloadedPuSupplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.Id == puSupplierProduct.Id);

        Assert.Equal(maximaCanonical.Id, puSource.CanonicalItemId);
        Assert.Equal("refreshed_cross_supplier_brand_raw_mfg_part", puSource.MatchMethod);
        Assert.Equal(maximaCanonical.Id, puOffer.CanonicalItemId);
        Assert.Equal(maximaCanonical.Id, reloadedPuSupplierProduct.CanonicalItemId);
    }

    [Fact]
    public async Task Product_matching_creates_manual_review_item_when_no_confident_match_exists()
    {
        var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Turn14", Code = "TURN14" };
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var service = new ProductMatchingService(dbContext);

        var result = await service.MatchAsync(new ProductMatchRequest(
            supplier.Id,
            "T14-NEW",
            Upc: null,
            ManufacturerPartNumber: "NEW-1",
            Brand: "New Brand",
            SupplierDescription: "New supplier part"),
            CancellationToken.None);

        Assert.Equal(ProductMatchType.ManualReview, result.MatchType);
        Assert.True(result.RequiresManualReview);
        Assert.NotNull(result.ReviewItemId);

        var reviewItem = await dbContext.ProductMatchReviewItems.SingleAsync();
        Assert.Equal(supplier.Id, reviewItem.SupplierId);
        Assert.Equal("T14-NEW", reviewItem.SupplierSku);
        Assert.Equal("Open", reviewItem.Status);
        Assert.Equal("No confident global product match was found.", reviewItem.MatchReason);
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
}
