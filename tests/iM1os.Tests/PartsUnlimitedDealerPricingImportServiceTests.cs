using System.Net;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class PartsUnlimitedDealerPricingImportServiceTests
{
    [Fact]
    public async Task Dealer_pricing_import_batches_part_numbers_and_upserts_company_cost()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var (supplierProduct, importRun) = await SeedImportRunAsync(dbContext, organizationId);
        var httpClientFactory = new RecordingHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
{
  "pricing": [
    { "partNumber": "003106", "dealerCost": 12.34, "currency": "USD" }
  ]
}
""")
        });
        var service = new PartsUnlimitedDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<PartsUnlimitedDealerPricingImportService>.Instance);

        var result = await service.ImportAsync(new PartsUnlimitedDealerPricingImportRequest(importRun.Id), CancellationToken.None);

        Assert.Equal(1, result.RowsProcessed);
        Assert.Equal(1, result.PricesUpserted);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal(["https://api.parts-unlimited.test/api/v1/parts/pricing/003106"], httpClientFactory.RequestUris);
        Assert.Equal(["company-token"], httpClientFactory.ApiKeys);

        var companyPrice = await dbContext.CompanySupplierPrices.AsNoTracking().SingleAsync();
        Assert.Equal(organizationId, companyPrice.OrganizationId);
        Assert.Equal(supplierProduct.Id, companyPrice.SupplierProductId);
        Assert.Equal("003106", companyPrice.SupplierSku);
        Assert.Equal(12.34m, companyPrice.ActualDealerCost);
        Assert.Equal("USD", companyPrice.Currency);
    }

    [Fact]
    public async Task Dealer_pricing_import_includes_response_body_when_parts_unlimited_rejects_request()
    {
        var organizationId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        var (_, importRun) = await SeedImportRunAsync(dbContext, organizationId);
        var httpClientFactory = new RecordingHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"message":"part_numbers required"}""")
        });
        var service = new PartsUnlimitedDealerPricingImportService(
            dbContext,
            httpClientFactory,
            new TestClock(),
            NullLogger<PartsUnlimitedDealerPricingImportService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportAsync(new PartsUnlimitedDealerPricingImportRequest(importRun.Id), CancellationToken.None));

        Assert.Contains("HTTP 400", exception.Message);
        Assert.Contains("part_numbers required", exception.Message);
        Assert.Equal(["https://api.parts-unlimited.test/api/v1/parts/pricing/003106"], httpClientFactory.RequestUris);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(), new TenantProvider(currentUser));
    }

    private static async Task<(SupplierProduct SupplierProduct, CompanySupplierConnectorImportRun ImportRun)> SeedImportRunAsync(
        ApplicationDbContext dbContext,
        Guid organizationId)
    {
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
        var configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = "PU",
            DisplayName = "Parts Unlimited",
            BaseApiUrl = "https://api.parts-unlimited.test/api",
            ApiKey = "company-token",
            AuthMode = "API Key",
            IsEnabled = true
        };
        var importRun = new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = "PartsUnlimitedDealerPricing",
            Status = "Queued",
            RequestedAtUtc = new DateTimeOffset(2026, 6, 30, 16, 33, 0, TimeSpan.Zero),
            ParametersJson = """{"MaxItems":500}"""
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(product);
        dbContext.SupplierProducts.Add(supplierProduct);
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        dbContext.CompanySupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();
        return (supplierProduct, importRun);
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 6, 30, 16, 34, 0, TimeSpan.Zero);
    }

    private sealed class RecordingHttpClientFactory(Func<string, HttpResponseMessage> responseFactory) : IHttpClientFactory
    {
        private readonly RecordingHttpMessageHandler handler = new(responseFactory);

        public IReadOnlyList<string> RequestUris => handler.RequestUris;

        public IReadOnlyList<string?> ApiKeys => handler.ApiKeys;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHttpMessageHandler(Func<string, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = [];

        public List<string?> ApiKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            RequestUris.Add(requestUri);
            ApiKeys.Add(request.Headers.TryGetValues("api-key", out var values) ? values.SingleOrDefault() : null);
            return Task.FromResult(responseFactory(requestUri));
        }
    }
}
