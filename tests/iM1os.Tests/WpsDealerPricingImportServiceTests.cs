using System.Net;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class WpsDealerPricingImportServiceTests
{
    [Fact]
    public async Task Dealer_pricing_import_polls_for_file_downloads_csv_and_upserts_company_cost()
    {
        var organizationId = Guid.NewGuid();
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
        var configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            ApiKey = "company-token",
            AuthMode = "API Key",
            IsEnabled = true
        };
        var importRun = new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = "WpsDealerPricing",
            Status = "Queued",
            RequestedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero),
            ParametersJson = """{"MaxItems":10}"""
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        dbContext.CompanySupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.Accepted, string.Empty, "application/json"),
            new(HttpStatusCode.OK, """{"url":"https://files.wps.test/dealer-pricing.csv"}""", "application/json"),
            new(HttpStatusCode.OK, "ItemNumber,DealerCost,Currency\r\n020-00104,92.18,USD\r\n", "text/csv"));
        var service = new WpsDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<WpsDealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new WpsDealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.RowsProcessed);
        Assert.Equal(1, result.PricesUpserted);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal(
            ["https://api.wps.test/dealer-pricing", "https://api.wps.test/dealer-pricing", "https://files.wps.test/dealer-pricing.csv"],
            httpClientFactory.RequestUris);
        Assert.Equal(["Bearer company-token", "Bearer company-token", null], httpClientFactory.AuthorizationHeaders);

        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal(organizationId, companyPrice.OrganizationId);
        Assert.Equal(supplierProduct.Id, companyPrice.SupplierProductId);
        Assert.Equal("020-00104", companyPrice.SupplierSku);
        Assert.Equal(92.18m, companyPrice.ActualDealerCost);
        Assert.Equal("USD", companyPrice.Currency);
        Assert.Equal(new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero), companyPrice.LastSyncedAtUtc);
    }

    [Fact]
    public async Task Dealer_pricing_import_keeps_polling_when_success_response_has_no_file_location_yet()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var importRun = await SeedImportRunAsync(dbContext, organizationId);
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"message":"File is still being generated."}""", "application/json"),
            new(HttpStatusCode.OK, """{"file_url":"https://files.wps.test/dealer-pricing.csv"}""", "application/json"),
            new(HttpStatusCode.OK, "ItemNumber,DealerCost,Currency\r\n020-00104,92.18,USD\r\n", "text/csv"));
        var service = new WpsDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<WpsDealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new WpsDealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.PricesUpserted);
        Assert.Equal(
            ["https://api.wps.test/dealer-pricing", "https://api.wps.test/dealer-pricing", "https://files.wps.test/dealer-pricing.csv"],
            httpClientFactory.RequestUris);
    }

    [Fact]
    public async Task Dealer_pricing_import_parses_wps_actual_dealer_price_header()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var importRun = await SeedImportRunAsync(dbContext, organizationId);
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"url":"https://files.wps.test/dealer-pricing.csv"}""", "application/json"),
            new(HttpStatusCode.OK, "id,sku,actual_dealer_price,standard_dealer_price,list_price,drop_ship_eligible,drop_ship_fee\r\n415,020-00104,92.18,95.00,126.95,Y,12.75\r\n", "text/csv"));
        var service = new WpsDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<WpsDealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new WpsDealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.RowsProcessed);
        Assert.Equal(1, result.PricesUpserted);
        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal("020-00104", companyPrice.SupplierSku);
        Assert.Equal("415", companyPrice.SourceSupplierProductId);
        Assert.Equal(92.18m, companyPrice.ActualDealerCost);
    }

    [Fact]
    public async Task Dealer_pricing_import_can_match_by_file_id_when_sku_differs()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var importRun = await SeedImportRunAsync(dbContext, organizationId);
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"url":"https://files.wps.test/dealer-pricing.csv"}""", "application/json"),
            new(HttpStatusCode.OK, "id,sku,actual_dealer_price,standard_dealer_price,list_price,drop_ship_eligible,drop_ship_fee\r\n415,DIFFERENT-SKU,92.18,95.00,126.95,Y,12.75\r\n", "text/csv"));
        var service = new WpsDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<WpsDealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new WpsDealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.PricesUpserted);
        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal("020-00104", companyPrice.SupplierSku);
        Assert.Equal("415", companyPrice.SourceSupplierProductId);
        Assert.Equal(92.18m, companyPrice.ActualDealerCost);
    }

    [Fact]
    public async Task Dealer_pricing_max_items_limits_upserts_not_raw_file_rows()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var importRun = await SeedImportRunAsync(dbContext, organizationId);
        importRun.ParametersJson = """{"MaxItems":1}""";
        await dbContext.SaveChangesAsync();
        var csv = """
id,sku,actual_dealer_price,standard_dealer_price,list_price,drop_ship_eligible,drop_ship_fee
999999,UNMATCHED-1,1.00,1.00,2.00,Y,12.75
888888,UNMATCHED-2,2.00,2.00,3.00,Y,12.75
415,020-00104,92.18,95.00,126.95,Y,12.75
777777,UNMATCHED-3,3.00,3.00,4.00,Y,12.75
""";
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"url":"https://files.wps.test/dealer-pricing.csv"}""", "application/json"),
            new(HttpStatusCode.OK, csv, "text/csv"));
        var service = new WpsDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<WpsDealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new WpsDealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(3, result.RowsProcessed);
        Assert.Equal(1, result.PricesUpserted);
        Assert.Equal(2, result.UnmatchedRows);
        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal(92.18m, companyPrice.ActualDealerCost);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(), new TenantProvider(currentUser));
    }

    private static async Task<CompanySupplierConnectorImportRun> SeedImportRunAsync(ApplicationDbContext dbContext, Guid organizationId)
    {
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
        var configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = "WPS",
            DisplayName = "WPS",
            BaseApiUrl = "https://api.wps.test",
            ApiKey = "company-token",
            AuthMode = "API Key",
            IsEnabled = true
        };
        var importRun = new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = "WpsDealerPricing",
            Status = "Queued",
            RequestedAtUtc = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero),
            ParametersJson = """{"MaxItems":10}"""
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        dbContext.CompanySupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();
        return importRun;
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class SequenceHttpClientFactory(params SequenceResponse[] responses) : IHttpClientFactory
    {
        private readonly Queue<SequenceResponse> responses = new(responses);

        public List<string> RequestUris { get; } = [];

        public List<string?> AuthorizationHeaders { get; } = [];

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new SequenceHandler(responses, RequestUris, AuthorizationHeaders));
        }
    }

    private sealed class SequenceHandler(
        Queue<SequenceResponse> responses,
        List<string> requestUris,
        List<string?> authorizationHeaders) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            authorizationHeaders.Add(request.Headers.Authorization is null
                ? null
                : $"{request.Headers.Authorization.Scheme} {request.Headers.Authorization.Parameter}");
            var response = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, null, response.ContentType)
            });
        }
    }

    private sealed record SequenceResponse(HttpStatusCode StatusCode, string Body, string ContentType);
}
