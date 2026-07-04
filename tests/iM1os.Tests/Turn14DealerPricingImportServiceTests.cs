using System.Net;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class Turn14DealerPricingImportServiceTests
{
    [Fact]
    public async Task Dealer_pricing_import_matches_pricing_id_to_supplier_sku_when_source_item_id_is_missing()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var (supplierProduct, importRun) = await SeedImportRunAsync(dbContext, organizationId, ["078595"], maxItems: 10);
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"access_token":"token"}""", "application/json"),
            new(HttpStatusCode.OK, """
{
  "data": [
    { "id": "078595", "type": "pricing", "attributes": { "purchase_cost": 29.96 } }
  ],
  "meta": { "total_pages": 1 },
  "links": { "self": "/v1/pricing", "last": "/v1/pricing", "next": null }
}
""", "application/json"));
        var service = new Turn14DealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<Turn14DealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new Turn14DealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.RowsProcessed);
        Assert.Equal(1, result.PricesUpserted);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal(
            ["https://api.turn14.test/v1/token", "https://api.turn14.test/v1/pricing"],
            httpClientFactory.RequestUris);
        Assert.Equal([null, "Bearer token"], httpClientFactory.AuthorizationHeaders);

        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal(organizationId, companyPrice.OrganizationId);
        Assert.Equal(supplierProduct.Id, companyPrice.SupplierProductId);
        Assert.Equal("078595", companyPrice.SupplierSku);
        Assert.Equal("078595", companyPrice.SourceSupplierProductId);
        Assert.Equal(29.96m, companyPrice.ActualDealerCost);
    }

    [Fact]
    public async Task Dealer_pricing_import_follows_turn14_next_links_until_max_items_are_processed()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var (_, importRun) = await SeedImportRunAsync(dbContext, organizationId, ["078595", "078596"], maxItems: 2);
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"access_token":"token"}""", "application/json"),
            new(HttpStatusCode.OK, """
{
  "data": [
    { "id": "078595", "type": "pricing", "attributes": { "purchase_cost": 29.96 } }
  ],
  "meta": { "total_pages": 2 },
  "links": { "self": "/v1/pricing", "last": "/v1/pricing?page=2", "next": "/v1/pricing?page=2" }
}
""", "application/json"),
            new(HttpStatusCode.OK, """
{
  "data": [
    { "id": "078596", "type": "pricing", "attributes": { "purchase_cost": 31.25 } }
  ],
  "meta": { "total_pages": 2 },
  "links": { "self": "/v1/pricing?page=2", "last": "/v1/pricing?page=2", "next": null }
}
""", "application/json"));
        var service = new Turn14DealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<Turn14DealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new Turn14DealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(2, result.RowsProcessed);
        Assert.Equal(2, result.PricesUpserted);
        Assert.Equal(
            ["https://api.turn14.test/v1/token", "https://api.turn14.test/v1/pricing", "https://api.turn14.test/v1/pricing?page=2"],
            httpClientFactory.RequestUris);
        var prices = await dbContext.CompanySupplierPrices.AsNoTracking().OrderBy(x => x.SupplierSku).ToListAsync();
        Assert.Collection(
            prices,
            price =>
            {
                Assert.Equal("078595", price.SupplierSku);
                Assert.Equal(29.96m, price.ActualDealerCost);
            },
            price =>
            {
                Assert.Equal("078596", price.SupplierSku);
                Assert.Equal(31.25m, price.ActualDealerCost);
            });
    }

    [Fact]
    public async Task Dealer_pricing_import_refreshes_token_and_retries_pricing_page_after_unauthorized_response()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var (supplierProduct, importRun) = await SeedImportRunAsync(dbContext, organizationId, ["078595"], maxItems: 1);
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"access_token":"token-1"}""", "application/json"),
            new(HttpStatusCode.Unauthorized, """{"error":"invalid_token","error_description":"The access token provided has expired"}""", "application/json"),
            new(HttpStatusCode.OK, """{"access_token":"token-2"}""", "application/json"),
            new(HttpStatusCode.OK, """
{
  "data": [
    { "id": "078595", "type": "pricing", "attributes": { "purchase_cost": 29.96 } }
  ],
  "meta": { "total_pages": 1 },
  "links": { "self": "/v1/pricing", "last": "/v1/pricing", "next": null }
}
""", "application/json"));
        var service = new Turn14DealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<Turn14DealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new Turn14DealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.RowsProcessed);
        Assert.Equal(1, result.PricesUpserted);
        Assert.Equal(
            ["https://api.turn14.test/v1/token", "https://api.turn14.test/v1/pricing", "https://api.turn14.test/v1/token", "https://api.turn14.test/v1/pricing"],
            httpClientFactory.RequestUris);
        Assert.Equal([null, "Bearer token-1", null, "Bearer token-2"], httpClientFactory.AuthorizationHeaders);

        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal(supplierProduct.Id, companyPrice.SupplierProductId);
        Assert.Equal(29.96m, companyPrice.ActualDealerCost);
    }

    [Fact]
    public async Task Dealer_pricing_import_normalizes_base_api_url_without_scheme()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var (_, importRun) = await SeedImportRunAsync(dbContext, organizationId, ["078595"], maxItems: 1, baseApiUrl: "api.turn14.test");
        var httpClientFactory = new SequenceHttpClientFactory(
            new(HttpStatusCode.OK, """{"access_token":"token"}""", "application/json"),
            new(HttpStatusCode.OK, """
{
  "data": [
    { "id": "078595", "type": "pricing", "attributes": { "purchase_cost": 29.96 } }
  ],
  "meta": { "total_pages": 1 },
  "links": { "self": "/v1/pricing", "last": "/v1/pricing", "next": null }
}
""", "application/json"));
        var service = new Turn14DealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<Turn14DealerPricingImportService>.Instance);

        await service.ImportAsync(new Turn14DealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(
            ["https://api.turn14.test/v1/token", "https://api.turn14.test/v1/pricing"],
            httpClientFactory.RequestUris);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(), new TenantProvider(currentUser));
    }

    private static async Task<(SupplierProduct FirstSupplierProduct, CompanySupplierConnectorImportRun ImportRun)> SeedImportRunAsync(
        ApplicationDbContext dbContext,
        Guid organizationId,
        IReadOnlyCollection<string> supplierSkus,
        int maxItems,
        string baseApiUrl = "https://api.turn14.test")
    {
        var supplier = new Supplier { Name = "Turn14", Code = "TURN14", ConnectorKey = "TURN14" };
        var configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = "TURN14",
            DisplayName = "Turn14",
            BaseApiUrl = baseApiUrl,
            ApiKey = "company-client",
            ApiSecretProtected = "company-secret",
            AuthMode = "OAuth",
            IsEnabled = true
        };
        var importRun = new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = "Turn14DealerPricing",
            Status = "Queued",
            RequestedAtUtc = new DateTimeOffset(2026, 6, 30, 17, 6, 0, TimeSpan.Zero),
            ParametersJson = $$"""{"MaxItems":{{maxItems}}}"""
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        dbContext.CompanySupplierConnectorImportRuns.Add(importRun);

        SupplierProduct? firstSupplierProduct = null;
        foreach (var sku in supplierSkus)
        {
            var product = new GlobalProduct
            {
                Brand = "ACME",
                Description = $"Part {sku}",
                Status = "Active"
            };
            var supplierProduct = new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = product.Id,
                SupplierSku = sku,
                SupplierPartNumber = sku,
                ManufacturerPartNumber = sku,
                SupplierStatus = "Active"
            };
            firstSupplierProduct ??= supplierProduct;
            dbContext.GlobalProducts.Add(product);
            dbContext.SupplierProducts.Add(supplierProduct);
        }

        await dbContext.SaveChangesAsync();
        return (firstSupplierProduct!, importRun);
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 30, 17, 7, 0, TimeSpan.Zero);
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
