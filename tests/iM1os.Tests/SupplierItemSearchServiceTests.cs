using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

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
        var formattedTitleResult = await service.SearchAsync("bolt m6/m8", 10, CancellationToken.None);

        Assert.Single(skuResult.Results);
        Assert.Single(mfgResult.Results);
        var item = Assert.Single(titleResult.Results);
        Assert.Single(formattedTitleResult.Results);
        Assert.Equal("020-00010", item.SupplierSku);
        Assert.Equal("TC-M6M8", item.ManufacturerPartNumber);
        Assert.Equal("BOLT M6/M8 THREAD CHASERS - TC-M6M8", item.Title);
        Assert.Equal(4.99m, item.Msrp);
        Assert.Equal(3.26m, item.DealerCost);
        Assert.Equal(1, item.FitmentRecordCount);
        Assert.Equal("https://cdn.example.test/thread-chaser.jpg", item.ImageUrl);
    }

    [Fact]
    public async Task Search_title_formatter_does_not_duplicate_existing_maker_or_part_number()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "59901-10",
            Description = "MAXIMA FORK FLUID 10W LITER - 59901-10",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "59901-10",
            SupplierDescription = "MAXIMA FORK FLUID 10W LITER - 59901-10",
            SupplierPartNumber = "59901-10",
            ManufacturerPartNumber = "59901-10",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync("MAXIMA FORK", 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("MAXIMA FORK FLUID 10W LITER - 59901-10", item.Title);
    }

    [Fact]
    public async Task Search_matches_multi_token_catalog_terms_across_brand_and_title()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var matchingProduct = new GlobalProduct
        {
            Brand = "DUNLOP",
            Manufacturer = "DUNLOP",
            ManufacturerPartNumber = "45273513",
            NormalizedManufacturerPartNumber = "45273513",
            Description = "TIRE GEOMAX MX34 REAR 110/100-18 64M BIAS TT",
            Status = "Active"
        };
        var unrelatedProduct = new GlobalProduct
        {
            Brand = "DUNLOP",
            Manufacturer = "DUNLOP",
            ManufacturerPartNumber = "45273599",
            NormalizedManufacturerPartNumber = "45273599",
            Description = "TIRE GEOMAX MX53 REAR 110/100-18 64M BIAS TT",
            Status = "Active"
        };
        var matchingSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = matchingProduct.Id,
            SupplierSku = "873-0790",
            SupplierDescription = "TIRE GEOMAX MX34 REAR 110/100-18 64M BIAS TT",
            SupplierPartNumber = "873-0790",
            ManufacturerPartNumber = "45273513",
            NormalizedManufacturerPartNumber = "45273513",
            SupplierStatus = "Active"
        };
        var unrelatedSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = unrelatedProduct.Id,
            SupplierSku = "873-0799",
            SupplierDescription = "TIRE GEOMAX MX53 REAR 110/100-18 64M BIAS TT",
            SupplierPartNumber = "873-0799",
            ManufacturerPartNumber = "45273599",
            NormalizedManufacturerPartNumber = "45273599",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(matchingProduct, unrelatedProduct);
        dbContext.SupplierProducts.AddRange(matchingSupplierProduct, unrelatedSupplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var brandAndModelPage = await service.SearchAsync("Dunlop MX34 18", 10, CancellationToken.None);
        var modelAndSizePage = await service.SearchAsync("MX34 18", 10, CancellationToken.None);

        var brandAndModelItem = Assert.Single(brandAndModelPage.Results);
        var modelAndSizeItem = Assert.Single(modelAndSizePage.Results);
        Assert.Equal("873-0790", brandAndModelItem.SupplierSku);
        Assert.Equal("873-0790", modelAndSizeItem.SupplierSku);
    }

    [Fact]
    public async Task Search_expands_singular_and_plural_text_tokens()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var product = new GlobalProduct
        {
            Brand = "ALL BALLS",
            Manufacturer = "ALL BALLS",
            ManufacturerPartNumber = "25-1234",
            Description = "Wheel Bearing Kit",
            Category = "Bearings",
            Status = "Active"
        };
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = product.Id,
            SupplierSku = "22-1234",
            SupplierDescription = "WHEEL BEARING KIT",
            SupplierPartNumber = "22-1234",
            ManufacturerPartNumber = "25-1234",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync("wheel bearings", 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("22-1234", item.SupplierSku);
    }

    [Fact]
    public async Task Search_filters_by_structured_tire_fields()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var matchingProduct = new GlobalProduct
        {
            Brand = "Dunlop",
            Manufacturer = "Dunlop",
            ManufacturerPartNumber = "45273518",
            Description = "Dunlop Geomax MX34 Rear Tire - 90/100-18 M/C 54M TT",
            TireWidth = 90,
            TireAspectRatio = 100,
            TireRimDiameter = 18,
            TirePosition = "rear",
            TireConstruction = "bias",
            TireType = "MX/offroad",
            TireModelLine = "MX34",
            Status = "Active"
        };
        var unrelatedProduct = new GlobalProduct
        {
            Brand = "Dunlop",
            Manufacturer = "Dunlop",
            ManufacturerPartNumber = "45273506",
            Description = "Dunlop Geomax MX34 Front Tire - 70/100-14 M/C 37M TT",
            TireWidth = 70,
            TireAspectRatio = 100,
            TireRimDiameter = 14,
            TirePosition = "front",
            TireConstruction = "bias",
            TireType = "MX/offroad",
            TireModelLine = "MX34",
            Status = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(matchingProduct, unrelatedProduct);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = matchingProduct.Id,
                SupplierSku = "dun45273518",
                SupplierDescription = matchingProduct.Description,
                SupplierPartNumber = "dun45273518",
                ManufacturerPartNumber = "45273518",
                SupplierStatus = "Active"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = unrelatedProduct.Id,
                SupplierSku = "dun45273506",
                SupplierDescription = unrelatedProduct.Description,
                SupplierPartNumber = "dun45273506",
                ManufacturerPartNumber = "45273506",
                SupplierStatus = "Active"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync(
            new SupplierItemSearchRequest(
                null,
                null,
                null,
                null,
                null,
                null,
                TireBrand: "Dunlop",
                TireModelLine: "MX34",
                TireRimDiameter: 18,
                TirePosition: "rear"),
            10,
            CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("dun45273518", item.SupplierSku);
    }

    [Fact]
    public async Task Search_sorts_exact_matches_before_partial_matches()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var exactProduct = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "22916",
            Description = "FORMULA K2 16OZ",
            Status = "Active"
        };
        var partialProduct = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "ABC-22916-KIT",
            Description = "FORMULA K2 DISPLAY KIT",
            Status = "Active"
        };
        var exactSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = exactProduct.Id,
            SupplierSku = "22916",
            SupplierDescription = "FORMULA K2 16OZ",
            SupplierPartNumber = "22916",
            ManufacturerPartNumber = "22916",
            NormalizedManufacturerPartNumber = "22916",
            SupplierStatus = "Active"
        };
        var partialSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = partialProduct.Id,
            SupplierSku = "ABC-22916-KIT",
            SupplierDescription = "FORMULA K2 DISPLAY KIT 22916",
            SupplierPartNumber = "ABC-22916-KIT",
            ManufacturerPartNumber = "ABC-22916-KIT",
            NormalizedManufacturerPartNumber = "ABC22916KIT",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(exactProduct, partialProduct);
        dbContext.SupplierProducts.AddRange(partialSupplierProduct, exactSupplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync("22916", 10, CancellationToken.None);

        Assert.Equal(2, page.Results.Count);
        Assert.Equal("22916", page.Results.First().SupplierSku);
        Assert.Equal("MAXIMA FORMULA K2 16OZ - 22916", page.Results.First().Title);
    }

    [Fact]
    public async Task Search_uses_manufacturer_as_secondary_sort()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var maximaProduct = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "22916",
            Description = "FORMULA K2 16OZ",
            Status = "Active"
        };
        var belRayProduct = new GlobalProduct
        {
            Brand = "BEL-RAY",
            Manufacturer = "BEL-RAY",
            ManufacturerPartNumber = "99210",
            Description = "FORMULA K2 16OZ",
            Status = "Active"
        };
        var maximaSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = maximaProduct.Id,
            SupplierSku = "M-22916",
            SupplierDescription = "FORMULA K2 16OZ",
            SupplierPartNumber = "M-22916",
            ManufacturerPartNumber = "22916",
            SupplierStatus = "Active"
        };
        var belRaySupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = belRayProduct.Id,
            SupplierSku = "B-99210",
            SupplierDescription = "FORMULA K2 16OZ",
            SupplierPartNumber = "B-99210",
            ManufacturerPartNumber = "99210",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(maximaProduct, belRayProduct);
        dbContext.SupplierProducts.AddRange(maximaSupplierProduct, belRaySupplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync("FORMULA K2 16OZ", 10, CancellationToken.None);

        Assert.Equal(2, page.Results.Count);
        Assert.Equal("BEL-RAY", page.Results.First().Brand);
        Assert.Equal("MAXIMA", page.Results.Last().Brand);
    }

    [Fact]
    public async Task Search_groups_known_brand_aliases_when_mfg_part_matches()
    {
        await using var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var turn14 = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var wpsProduct = new GlobalProduct
        {
            Brand = "ALL BALLS",
            Manufacturer = "ALL BALLS",
            ManufacturerPartNumber = "57-105",
            NormalizedManufacturerPartNumber = "57105",
            Description = "Fork Dust Seal Kit",
            Status = "Active"
        };
        var turn14Product = new GlobalProduct
        {
            Brand = "All Balls Racing",
            Manufacturer = "All Balls Racing",
            ManufacturerPartNumber = "57-105",
            NormalizedManufacturerPartNumber = "57105",
            Description = "Fork Dust Seal Kit",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "22-57105",
            SupplierDescription = "ALL BALLS FORK DUST SEAL KIT",
            SupplierPartNumber = "22-57105",
            ManufacturerPartNumber = "57-105",
            NormalizedManufacturerPartNumber = "57105",
            SupplierStatus = "Active"
        };
        var turn14SupplierProduct = new SupplierProduct
        {
            SupplierId = turn14.Id,
            GlobalProductId = turn14Product.Id,
            SupplierSku = "abr57-105",
            SupplierDescription = "ALL BALLS RACING FORK DUST SEAL KIT",
            SupplierPartNumber = "abr57-105",
            ManufacturerPartNumber = "57-105",
            NormalizedManufacturerPartNumber = "57105",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.AddRange(wps, turn14);
        dbContext.GlobalProducts.AddRange(wpsProduct, turn14Product);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, turn14SupplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync("57-105", 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.NotNull(item.Offers);
        Assert.Equal(2, item.Offers!.Count);
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "WPS" && offer.SupplierSku == "22-57105");
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "TURN14" && offer.SupplierSku == "abr57-105");
    }

    [Fact]
    public async Task Search_groups_maxima_racing_oil_alias_when_normalized_mfg_part_matches()
    {
        await using var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var pu = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var wpsProduct = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            Description = "Pro Plus Oil",
            Status = "Active"
        };
        var puProduct = new GlobalProduct
        {
            Brand = "MAXIMA RACING OIL",
            Manufacturer = "MAXIMA RACING OIL",
            ManufacturerPartNumber = "3002901",
            NormalizedManufacturerPartNumber = "3002901",
            Description = "Oil 4T Pro Plus",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "78-98686",
            SupplierDescription = "MAXIMA PRO PLUS OIL",
            SupplierPartNumber = "78-98686",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            SupplierStatus = "Active"
        };
        var puSupplierProduct = new SupplierProduct
        {
            SupplierId = pu.Id,
            GlobalProductId = puProduct.Id,
            SupplierSku = "36010269",
            SupplierDescription = "OIL 4T PRO PLUS+ 10W40 L",
            SupplierPartNumber = "36010269",
            ManufacturerPartNumber = "3002901",
            NormalizedManufacturerPartNumber = "3002901",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.AddRange(wps, pu);
        dbContext.GlobalProducts.AddRange(wpsProduct, puProduct);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, puSupplierProduct);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync("30-02901", 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.NotNull(item.Offers);
        Assert.Equal(2, item.Offers!.Count);
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "WPS" && offer.ManufacturerPartNumber == "30-02901");
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "PU" && offer.ManufacturerPartNumber == "3002901");
    }

    [Fact]
    public async Task Company_search_prefers_configured_supplier_when_available()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var turn14 = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var wpsProduct = new GlobalProduct
        {
            Brand = "ACERBIS",
            Manufacturer = "ACERBIS",
            ManufacturerPartNumber = "2113770001",
            NormalizedManufacturerPartNumber = "2113770001",
            Description = "Fork Guard Black",
            Status = "Active"
        };
        var turn14Product = new GlobalProduct
        {
            Brand = "ACERBIS",
            Manufacturer = "ACERBIS",
            ManufacturerPartNumber = "2113770001",
            NormalizedManufacturerPartNumber = "2113770001",
            Description = "Fork Guard Black",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "21137-70001",
            SupplierDescription = "ACERBIS FORK GUARD BLACK",
            ManufacturerPartNumber = "2113770001",
            NormalizedManufacturerPartNumber = "2113770001",
            WarehouseAvailability = """{"CA":4}""",
            SupplierStatus = "Active"
        };
        var turn14SupplierProduct = new SupplierProduct
        {
            SupplierId = turn14.Id,
            GlobalProductId = turn14Product.Id,
            SupplierSku = "acb2113770001",
            SupplierDescription = "ACERBIS FORK GUARD BLACK",
            ManufacturerPartNumber = "2113770001",
            NormalizedManufacturerPartNumber = "2113770001",
            WarehouseAvailability = """{"01":2}""",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.AddRange(wps, turn14);
        dbContext.GlobalProducts.AddRange(wpsProduct, turn14Product);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, turn14SupplierProduct);
        dbContext.SupplierPrices.AddRange(
            new SupplierPrice { SupplierProductId = wpsSupplierProduct.Id, Msrp = 39.95m, DealerCost = 20.77m },
            new SupplierPrice { SupplierProductId = turn14SupplierProduct.Id, Msrp = 39.95m, DealerCost = 25.97m });
        dbContext.TenantModuleEntitlements.AddRange(
            new TenantModuleEntitlement { OrganizationId = organizationId, ModuleKey = "SupplierConnector:WPS", IsEnabled = true },
            new TenantModuleEntitlement { OrganizationId = organizationId, ModuleKey = "SupplierConnector:TURN14", IsEnabled = true });
        dbContext.BusinessConfigurations.Add(new BusinessConfiguration
        {
            OrganizationId = organizationId,
            SupplierPreferencesJson = """{"preferredSupplierCode":"TURN14","preferredWarehouseCodes":{"TURN14":"01","WPS":"CA"}}"""
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchForCompanyAsync(organizationId, new SupplierItemSearchRequest("2113770001", null, null, null, null, null, SearchExecuted: true), 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("TURN14", item.SupplierCode);
        Assert.Equal("acb2113770001", item.SupplierSku);
        Assert.NotNull(item.Offers);
        Assert.Contains(item.Offers!, offer => offer.SupplierCode == "TURN14" && offer.IsDefaultOffer && offer.IsPreferredSupplier && offer.PreferredWarehouseCode == "01");
        Assert.Contains(item.Offers!, offer => offer.SupplierCode == "WPS" && !offer.IsDefaultOffer && offer.ActualCost is null);
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
    public async Task Ymm_supplier_search_includes_cross_supplier_mfg_part_references()
    {
        await using var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var partsUnlimited = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var wpsProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            Description = "Thread Chasers",
            Status = "Active"
        };
        var partsUnlimitedProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC M6 M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            Description = "Thread Chaser Kit",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "020-00104",
            SupplierDescription = "THREAD CHASERS",
            SupplierPartNumber = "020-00104",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            SupplierStatus = "STK"
        };
        var partsUnlimitedSupplierProduct = new SupplierProduct
        {
            SupplierId = partsUnlimited.Id,
            GlobalProductId = partsUnlimitedProduct.Id,
            SupplierSku = "PU-12345",
            SupplierDescription = "THREAD CHASER KIT",
            SupplierPartNumber = "PU-12345",
            ManufacturerPartNumber = "TC M6 M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.AddRange(wps, partsUnlimited);
        dbContext.GlobalProducts.AddRange(wpsProduct, partsUnlimitedProduct);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, partsUnlimitedSupplierProduct);
        dbContext.SupplierFitmentRecords.AddRange(
            new SupplierFitmentRecord
            {
                SupplierId = wps.Id,
                SupplierProductId = wpsSupplierProduct.Id,
                GlobalProductId = wpsProduct.Id,
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
                SupplierId = partsUnlimited.Id,
                SupplierProductId = partsUnlimitedSupplierProduct.Id,
                GlobalProductId = partsUnlimitedProduct.Id,
                SupplierKey = "PU",
                SupplierSku = "PU-12345",
                VehicleClass = "offroad_dirt",
                VehicleType = "Offroad / Dirt",
                Year = 2019,
                Make = "Yamaha",
                Model = "YZ250F",
                ResolutionStatus = "Resolved",
                ImportedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero)
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync(new SupplierItemSearchRequest(null, "WPS", "Offroad / Dirt", 2018, "KTM", "125 SX"), 10, CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.Equal("WPS", item.SupplierCode);
        Assert.Equal("020-00104", item.SupplierSku);
        Assert.False(item.IsCrossReference);
        Assert.Empty(item.CrossReferences);
        Assert.NotNull(item.Offers);
        Assert.Equal(2, item.Offers!.Count);
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "WPS" && offer.SupplierSku == "020-00104" && offer.IsDefaultOffer);
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "PU" && offer.SupplierSku == "PU-12345" && offer.ManufacturerPartNumber == "TC M6 M8");
        Assert.NotNull(item.Fitment);
        Assert.Equal(2, item.Fitment!.Count);
        Assert.Contains(item.Fitment, fitment => fitment.Year == 2018 && fitment.Make == "KTM" && fitment.Model == "125 SX" && fitment.SupplierCodes.Contains("WPS"));
        Assert.Contains(item.Fitment, fitment => fitment.Year == 2019 && fitment.Make == "Yamaha" && fitment.Model == "YZ250F" && fitment.SupplierCodes.Contains("PU"));
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

    [Fact]
    public async Task Search_normalized_catalog_returns_canonical_item_with_supplier_offers()
    {
        await using var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var partsUnlimited = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var canonicalItem = new CanonicalItem
        {
            Brand = "NGK",
            Manufacturer = "NGK",
            ManufacturerPartNumber = "BR8ES",
            NormalizedManufacturerPartNumber = "BR8ES",
            PrimaryUpc = "087295103968",
            Title = "NGK Spark Plug BR8ES",
            Category = "Ignition",
            PrimaryImageUrl = "https://cdn.example.test/br8es.jpg",
            SearchText = "ngk spark plug br8es",
            Status = "Active"
        };
        var wpsProduct = new GlobalProduct
        {
            Brand = "NGK",
            Manufacturer = "NGK",
            ManufacturerPartNumber = "BR-8ES",
            NormalizedManufacturerPartNumber = "BR8ES",
            Description = "Spark Plug BR-8ES",
            Status = "Active"
        };
        var partsUnlimitedProduct = new GlobalProduct
        {
            Brand = "NGK",
            Manufacturer = "NGK",
            ManufacturerPartNumber = "BR8ES",
            NormalizedManufacturerPartNumber = "BR8ES",
            Description = "NGK Standard Spark Plug",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "2103-0010",
            SupplierDescription = "NGK SPARK PLUG BR-8ES",
            SupplierPartNumber = "2103-0010",
            ManufacturerPartNumber = "BR-8ES",
            NormalizedManufacturerPartNumber = "BR8ES",
            SupplierStatus = "Active"
        };
        var partsUnlimitedSupplierProduct = new SupplierProduct
        {
            SupplierId = partsUnlimited.Id,
            GlobalProductId = partsUnlimitedProduct.Id,
            SupplierSku = "BR8ES",
            SupplierDescription = "NGK STANDARD SPARK PLUG",
            SupplierPartNumber = "BR8ES",
            ManufacturerPartNumber = "BR8ES",
            NormalizedManufacturerPartNumber = "BR8ES",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.AddRange(wps, partsUnlimited);
        dbContext.GlobalProducts.AddRange(wpsProduct, partsUnlimitedProduct);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, partsUnlimitedSupplierProduct);
        dbContext.CanonicalItems.Add(canonicalItem);
        dbContext.CanonicalItemSources.AddRange(
            new CanonicalItemSource
            {
                CanonicalItemId = canonicalItem.Id,
                GlobalProductId = wpsProduct.Id,
                SupplierId = wps.Id,
                SupplierProductId = wpsSupplierProduct.Id,
                SupplierCode = "WPS",
                SourceTable = "supplier_products",
                SourceKey = wpsSupplierProduct.Id.ToString(),
                MatchMethod = "normalized_mpn",
                MatchConfidence = 1m
            },
            new CanonicalItemSource
            {
                CanonicalItemId = canonicalItem.Id,
                GlobalProductId = partsUnlimitedProduct.Id,
                SupplierId = partsUnlimited.Id,
                SupplierProductId = partsUnlimitedSupplierProduct.Id,
                SupplierCode = "PU",
                SourceTable = "supplier_products",
                SourceKey = partsUnlimitedSupplierProduct.Id.ToString(),
                MatchMethod = "normalized_mpn",
                MatchConfidence = 1m
            });
        dbContext.CanonicalItemSupplierOffers.AddRange(
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = canonicalItem.Id,
                SupplierId = wps.Id,
                SupplierProductId = wpsSupplierProduct.Id,
                SupplierCode = "WPS",
                SupplierSku = "2103-0010",
                SupplierPartNumber = "BR-8ES",
                SupplierTitle = "NGK SPARK PLUG BR-8ES",
                ListPrice = 4.99m,
                DealerCost = 3.25m,
                Status = "Active"
            },
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = canonicalItem.Id,
                SupplierId = partsUnlimited.Id,
                SupplierProductId = partsUnlimitedSupplierProduct.Id,
                SupplierCode = "PU",
                SupplierSku = "BR8ES",
                SupplierPartNumber = "BR8ES",
                SupplierTitle = "NGK STANDARD SPARK PLUG",
                ListPrice = 4.99m,
                DealerCost = 3.15m,
                Status = "Active"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync(
            new SupplierItemSearchRequest("BR-8ES", null, null, null, null, null, SearchExecuted: true, UseNormalizedCatalog: true),
            10,
            CancellationToken.None);

        Assert.True(page.UseNormalizedCatalog);
        var item = Assert.Single(page.Results);
        Assert.Equal("NGK Spark Plug BR8ES", item.Title);
        Assert.Equal("BR8ES", item.ManufacturerPartNumber);
        Assert.Equal("087295103968", item.Upc);
        Assert.NotNull(item.Offers);
        Assert.Equal(2, item.Offers!.Count);
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "WPS" && offer.SupplierSku == "2103-0010");
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "PU" && offer.SupplierSku == "BR8ES");
    }

    [Fact]
    public async Task Search_normalized_catalog_filters_by_ymm_from_supplier_fitment_records()
    {
        await using var dbContext = CreateContext();
        var supplier = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var matchedCanonicalItem = new CanonicalItem
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            Title = "Thread Chasers",
            Status = "Active"
        };
        var otherCanonicalItem = new CanonicalItem
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M10",
            NormalizedManufacturerPartNumber = "TCM10",
            Title = "Other Thread Chasers",
            Status = "Active"
        };
        var matchedProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            Description = "Thread Chasers",
            Status = "Active"
        };
        var otherProduct = new GlobalProduct
        {
            Brand = "BOLT",
            Manufacturer = "BOLT",
            ManufacturerPartNumber = "TC-M10",
            NormalizedManufacturerPartNumber = "TCM10",
            Description = "Other Thread Chasers",
            Status = "Active"
        };
        var matchedSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = matchedProduct.Id,
            SupplierSku = "020-00104",
            SupplierDescription = "EURO STYLE TRACK PACK II",
            SupplierPartNumber = "020-00104",
            ManufacturerPartNumber = "TC-M6M8",
            NormalizedManufacturerPartNumber = "TCM6M8",
            SupplierStatus = "STK"
        };
        var otherSupplierProduct = new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = otherProduct.Id,
            SupplierSku = "020-00999",
            SupplierDescription = "OTHER PART",
            SupplierPartNumber = "020-00999",
            ManufacturerPartNumber = "TC-M10",
            NormalizedManufacturerPartNumber = "TCM10",
            SupplierStatus = "STK"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(matchedProduct, otherProduct);
        dbContext.SupplierProducts.AddRange(matchedSupplierProduct, otherSupplierProduct);
        dbContext.CanonicalItems.AddRange(matchedCanonicalItem, otherCanonicalItem);
        dbContext.CanonicalItemSupplierOffers.AddRange(
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = matchedCanonicalItem.Id,
                SupplierId = supplier.Id,
                SupplierProductId = matchedSupplierProduct.Id,
                SupplierCode = "WPS",
                SupplierSku = "020-00104",
                SupplierPartNumber = "020-00104",
                SupplierTitle = "EURO STYLE TRACK PACK II",
                Status = "STK"
            },
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = otherCanonicalItem.Id,
                SupplierId = supplier.Id,
                SupplierProductId = otherSupplierProduct.Id,
                SupplierCode = "WPS",
                SupplierSku = "020-00999",
                SupplierPartNumber = "020-00999",
                SupplierTitle = "OTHER PART",
                Status = "STK"
            });
        dbContext.SupplierFitmentRecords.Add(new SupplierFitmentRecord
        {
            SupplierId = supplier.Id,
            SupplierProductId = matchedSupplierProduct.Id,
            GlobalProductId = matchedProduct.Id,
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

        var page = await service.SearchAsync(
            new SupplierItemSearchRequest(null, null, "Offroad / Dirt", 2018, "KTM", "125 SX", SearchExecuted: true, UseNormalizedCatalog: true),
            10,
            CancellationToken.None);

        Assert.True(page.UseNormalizedCatalog);
        var item = Assert.Single(page.Results);
        Assert.Equal("020-00104", item.SupplierSku);
        Assert.Equal(1, item.FitmentRecordCount);
    }

    [Fact]
    public async Task Search_normalized_catalog_groups_alias_brands_by_normalized_mfg_part()
    {
        await using var dbContext = CreateContext();
        var wps = new Supplier { Name = "Western Power Sports", Code = "WPS", ConnectorKey = "WPS" };
        var partsUnlimited = new Supplier { Name = "Parts Unlimited", Code = "PU", ConnectorKey = "PU" };
        var turn14 = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14" };
        var maximaCanonicalItem = new CanonicalItem
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            Title = "MAXIMA PROPLUS OIL 4T 10W40 1L - 30-02901",
            Category = "Chemicals",
            Status = "Active"
        };
        var maximaRacingCanonicalItem = new CanonicalItem
        {
            Brand = "MAXIMA RACING OIL",
            Manufacturer = "MAXIMA RACING OIL",
            ManufacturerPartNumber = "3002901",
            NormalizedManufacturerPartNumber = "3002901",
            Title = "OIL 4T PRO PLUS+ 10W40 L - 3002901",
            Category = "A",
            Status = "Active"
        };
        var wpsProduct = new GlobalProduct
        {
            Brand = "MAXIMA",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            Description = "Pro Plus Oil",
            Status = "Active"
        };
        var partsUnlimitedProduct = new GlobalProduct
        {
            Brand = "MAXIMA RACING OIL",
            Manufacturer = "MAXIMA RACING OIL",
            ManufacturerPartNumber = "3002901",
            NormalizedManufacturerPartNumber = "3002901",
            Description = "OIL 4T PRO PLUS+ 10W40 L",
            Status = "Active"
        };
        var turn14Product = new GlobalProduct
        {
            Brand = "Maxima",
            Manufacturer = "MAXIMA",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            Description = "MXA Pro Plus+ Oil",
            Status = "Active"
        };
        var wpsSupplierProduct = new SupplierProduct
        {
            SupplierId = wps.Id,
            GlobalProductId = wpsProduct.Id,
            SupplierSku = "78-98686",
            SupplierDescription = "MAXIMA PRO PLUS OIL",
            SupplierPartNumber = "78-98686",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            SupplierStatus = "STK"
        };
        var partsUnlimitedSupplierProduct = new SupplierProduct
        {
            SupplierId = partsUnlimited.Id,
            GlobalProductId = partsUnlimitedProduct.Id,
            SupplierSku = "36010269",
            SupplierDescription = "OIL 4T PRO PLUS+ 10W40 L",
            SupplierPartNumber = "3601-0269",
            ManufacturerPartNumber = "3002901",
            NormalizedManufacturerPartNumber = "3002901",
            SupplierStatus = "Active"
        };
        var turn14SupplierProduct = new SupplierProduct
        {
            SupplierId = turn14.Id,
            GlobalProductId = turn14Product.Id,
            SupplierSku = "mxa30-02901",
            SupplierDescription = "MXA Pro Plus+ Oil",
            SupplierPartNumber = "mxa30-02901",
            ManufacturerPartNumber = "30-02901",
            NormalizedManufacturerPartNumber = "3002901",
            SupplierStatus = "Active"
        };

        dbContext.Suppliers.AddRange(wps, partsUnlimited, turn14);
        dbContext.GlobalProducts.AddRange(wpsProduct, partsUnlimitedProduct, turn14Product);
        dbContext.SupplierProducts.AddRange(wpsSupplierProduct, partsUnlimitedSupplierProduct, turn14SupplierProduct);
        dbContext.CanonicalItems.AddRange(maximaCanonicalItem, maximaRacingCanonicalItem);
        dbContext.CanonicalItemSupplierOffers.AddRange(
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = maximaCanonicalItem.Id,
                SupplierId = wps.Id,
                SupplierProductId = wpsSupplierProduct.Id,
                SupplierCode = "WPS",
                SupplierSku = "78-98686",
                SupplierPartNumber = "78-98686",
                SupplierTitle = "MAXIMA PRO PLUS OIL",
                ListPrice = 18.99m,
                DealerCost = 12.99m,
                Status = "STK"
            },
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = maximaRacingCanonicalItem.Id,
                SupplierId = partsUnlimited.Id,
                SupplierProductId = partsUnlimitedSupplierProduct.Id,
                SupplierCode = "PU",
                SupplierSku = "36010269",
                SupplierPartNumber = "3601-0269",
                SupplierTitle = "OIL 4T PRO PLUS+ 10W40 L",
                ListPrice = 18.99m,
                DealerCost = 12.99m,
                Status = "Active"
            },
            new CanonicalItemSupplierOffer
            {
                CanonicalItemId = maximaCanonicalItem.Id,
                SupplierId = turn14.Id,
                SupplierProductId = turn14SupplierProduct.Id,
                SupplierCode = "TURN14",
                SupplierSku = "mxa30-02901",
                SupplierPartNumber = "mxa30-02901",
                SupplierTitle = "MXA Pro Plus+ Oil",
                ListPrice = 227.88m,
                DealerCost = 120m,
                Status = "Active"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var page = await service.SearchAsync(
            new SupplierItemSearchRequest("30-02901", null, null, null, null, null, SearchExecuted: true, UseNormalizedCatalog: true),
            10,
            CancellationToken.None);

        var item = Assert.Single(page.Results);
        Assert.NotNull(item.Offers);
        Assert.Equal(3, item.Offers!.Count);
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "WPS" && offer.SupplierSku == "78-98686" && offer.ManufacturerPartNumber == "30-02901");
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "PU" && offer.SupplierSku == "36010269" && offer.ManufacturerPartNumber == "3002901");
        Assert.Contains(item.Offers, offer => offer.SupplierCode == "TURN14" && offer.SupplierSku == "mxa30-02901" && offer.ManufacturerPartNumber == "30-02901");
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
        return new SupplierItemSearchService(
            dbContext,
            new TenantModuleEntitlementService(dbContext),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<SupplierItemSearchService>.Instance);
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);
    }
}
