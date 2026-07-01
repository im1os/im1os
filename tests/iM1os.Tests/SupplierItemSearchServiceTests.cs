using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Platform;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class SupplierItemSearchServiceTests
{
    [Fact]
    public async Task Search_matches_supplier_sku_mfg_part_and_title()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            Description = "Thread Chasers",
            Category = "Hardware/Fasteners/Fittings",
            Upc = "819648023063",
            ImagesJson = """[{"url":"https://cdn.example.test/thread-chaser.jpg","isPrimary":true}]""",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00010",
            SupplierDescription = "M6/M8 THREAD CHASERS",
            SupplierPartNumber = "020-00010",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.SupplierPrices.Add(new SupplierPrice
        {
            SupplierProductId = supplierProduct.Id,
            Msrp = 4.99m,
            DealerCost = 3.26m,
            EffectiveDate = new DateOnly(2026, 6, 29),
            LastUpdated = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
        });
        dbContext.SupplierFitmentRecords.Add(new SupplierFitmentRecord
        {
            SupplierId = supplier.Id,
            SupplierProductId = supplierProduct.Id,
            GlobalProductId = product.Id,
            SupplierKey = "WPS",
            SupplierSku = "020-00010",
            SourceSupplierProductId = "100",
            SourceFitmentItemId = "100",
            Year = 2024,
            Make = "Honda",
            Model = "CRF450R",
            ResolutionStatus = "Resolved",
            ImportedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var skuResult = await service.SearchAsync("020-00010", 10, CancellationToken.None);
        var mfgResult = await service.SearchAsync("TC M6-M8", 10, CancellationToken.None);
        var titleResult = await service.SearchAsync("thread chasers", 10, CancellationToken.None);

        Assert.Single(skuResult.Results);
        Assert.Single(mfgResult.Results);
        var item = Assert.Single(titleResult.Results);
        Assert.Equal("020-00010", item.SupplierSku);
        Assert.Equal("TC-M6M8", item.ManufacturerPartNumber);
        Assert.Equal("M6/M8 THREAD CHASERS", item.Title);
        Assert.Equal(4.99m, item.Msrp);
        Assert.Equal(3.26m, item.DealerCost);
        Assert.Equal(1, item.FitmentRecordCount);
        Assert.Equal("https://cdn.example.test/thread-chaser.jpg", item.ImageUrl);
    }

    [Fact]
    public async Task Search_filters_by_year_make_model_and_returns_dependent_options()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "BOLT",
            Description = "Euro Style Track Pack II",
            Status = "Active"
        };
        var matchedSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00104",
            SupplierDescription = "EURO STYLE TRACK PACK II",
            SupplierStatus = "STK"
        };
        var otherProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Description = "Other Part",
            Status = "Active"
        };
        var otherSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = otherProduct.Id,
            SupplierSku = "020-00999",
            SupplierDescription = "OTHER PART",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(product, otherProduct);
        dbContext.SupplierProducts.AddRange(matchedSupplierProduct, otherSupplierProduct);
        dbContext.SupplierFitmentRecords.AddRange(
            new SupplierFitmentRecord
            {
                SupplierId = supplier.Id,
                SupplierProductId = matchedSupplierProduct.Id,
                GlobalProductId = product.Id,
                SupplierKey = "WPS",
                SupplierSku = "020-00104",
                VehicleClass = "offroad_dirt",
                VehicleType = "Offroad / Dirt",
                Year = 2018,
                Make = "KTM",
                Model = "125 SX",
                ResolutionStatus = "Resolved",
                ImportedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
            },
            new SupplierFitmentRecord
            {
                SupplierId = supplier.Id,
                SupplierProductId = otherSupplierProduct.Id,
                GlobalProductId = otherProduct.Id,
                SupplierKey = "WPS",
                SupplierSku = "020-00999",
                VehicleClass = "street",
                VehicleType = "Street",
                Year = 2018,
                Make = "Honda",
                Model = "CRF450R",
                ResolutionStatus = "Resolved",
                ImportedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync(new SupplierItemSearchRequest(null, null, "Offroad / Dirt", 2018, "KTM", "125 SX"), 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("020-00104", item.SupplierSku);
        Assert.Contains("Offroad / Dirt", page.AvailableVehicleTypes);
        Assert.Contains("Street", page.AvailableVehicleTypes);
        Assert.Contains(2018, page.AvailableYears);
        Assert.Contains("KTM", page.AvailableMakes);
        Assert.Contains("125 SX", page.AvailableModels);
        Assert.Equal(1, item.FitmentRecordCount);

        var mismatchedTypePage = await service.SearchAsync(new SupplierItemSearchRequest(null, null, "Street", 2018, "KTM", "125 SX"), 10, CancellationToken.None);
        Assert.Empty(mismatchedTypePage.Results);
    }

    [Fact]
    public async Task CountFitmentItemsForCompany_counts_enabled_supplier_fitment_items()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct { Brand = "BOLT", Description = "Euro Style Track Pack II", Status = "Active" };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "020-00104",
            SupplierDescription = "EURO STYLE TRACK PACK II",
            SupplierStatus = "STK"
        };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.TenantModuleEntitlements.Add(new TenantModuleEntitlement
        {
            OrganizationId = organizationId,
            ModuleKey = "SupplierConnector:WPS",
            IsEnabled = true
        });
        dbContext.SupplierFitmentRecords.Add(new SupplierFitmentRecord
        {
            SupplierId = supplier.Id,
            SupplierProductId = supplierProduct.Id,
            GlobalProductId = product.Id,
            SupplierKey = "WPS",
            SupplierSku = "020-00104",
            VehicleClass = "offroad_dirt",
            VehicleType = "Offroad / Dirt",
            Year = 2018,
            Make = "KTM",
            Model = "125 SX",
            ResolutionStatus = "Resolved",
            ImportedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var count = await service.CountFitmentItemsForCompanyAsync(organizationId, "Offroad / Dirt", 2018, "KTM", "125 SX", CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Search_can_filter_by_configured_supplier()
    {
        await using var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var turn14 = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var wpsProduct = new GlobalProduct { Brand = "BOLT", Description = "Thread Chasers", Status = "Active" };
        var turn14Product = new GlobalProduct { Brand = "APR", Description = "Brake Hoses", Status = "Active" };
        dbContext.Suppliers.AddRange(wps, turn14);
        dbContext.SupplierConnectorConfigurations.AddRange(
            new SupplierConnectorConfiguration
            {
                SupplierId = wps.Id,
                ConnectorKey = "WPS",
                DisplayName = "WPS",
                AuthMode = "API",
                IsEnabled = true
            },
            new SupplierConnectorConfiguration
            {
                SupplierId = turn14.Id,
                ConnectorKey = "TURN14",
                DisplayName = "Turn14",
                AuthMode = "API",
                IsEnabled = false
            });
        dbContext.GlobalProducts.AddRange(wpsProduct, turn14Product);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = wps.Id,
                GlobalProductId = wpsProduct.Id,
                SupplierSku = "020-00010",
                SupplierDescription = "THREAD CHASERS",
                SupplierStatus = "STK"
            },
            new SupplierProduct
            {
                SupplierId = turn14.Id,
                GlobalProductId = turn14Product.Id,
                SupplierSku = "aprBRK00047",
                SupplierDescription = "APR BRAKE HOSES",
                SupplierStatus = "Active"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync(new SupplierItemSearchRequest(null, "TURN14", null, null, null, null), 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("TURN14", item.SupplierCode);
        Assert.Equal("aprBRK00047", item.SupplierSku);
        Assert.Equal(2, page.ConfiguredSuppliers.Count);
        Assert.Contains(page.ConfiguredSuppliers, x => x.Code == "WPS" && x.IsEnabled);
        Assert.Contains(page.ConfiguredSuppliers, x => x.Code == "TURN14" && !x.IsEnabled);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(), new TenantProvider(currentUser));
    }

    private static SupplierItemSearchService CreateService(ApplicationDbContext dbContext)
    {
        return new SupplierItemSearchService(dbContext, new TenantModuleEntitlementService(dbContext));
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);
    }
}
