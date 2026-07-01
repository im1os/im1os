using System.Net;
using System.Text;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class Turn14MediaEnrichmentServiceTests
{
    [Fact]
    public async Task Import_enriches_turn14_products_missing_images_and_resumes_by_skipping_completed_rows()
    {
        var now = new DateTimeOffset(2026, 6, 30, 15, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14", IsActive = true };
        var globalProduct = new GlobalProduct { Brand = "ACME", Description = "Slip-On Exhaust", Status = "Active" };
        var enrichedGlobalProduct = new GlobalProduct { Brand = "ACME", Description = "Already Enriched", Status = "Active", ImagesJson = """[{"url":"existing"}]""" };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.AddRange(globalProduct, enrichedGlobalProduct);
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "TURN14",
            DisplayName = "Turn14",
            AuthMode = "CookieLogin",
            ApiKey = "turn14-client",
            ApiSecretProtected = """{"apiClientSecret":"turn14-secret"}""",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        dbContext.SupplierProducts.AddRange(
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = globalProduct.Id,
                SupplierSku = "ABC123",
                SupplierStatus = "Active"
            },
            new SupplierProduct
            {
                SupplierId = supplier.Id,
                GlobalProductId = enrichedGlobalProduct.Id,
                SupplierSku = "DONE123",
                SourceSupplierProductId = "item-done123",
                SupplierStatus = "Active",
                SupplierImagesJson = """[{"url":"existing"}]"""
            });
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "Turn14MediaEnrichment",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":10,"DelayMilliseconds":0}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var httpClientFactory = new RecordingHttpClientFactory();
        var service = new Turn14MediaEnrichmentService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<Turn14MediaEnrichmentService>.Instance);

        var result = await service.ImportAsync(new Turn14MediaEnrichmentRunRequest(importRun.Id, 10, 0), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.UpdatedProducts);
        Assert.Contains(httpClientFactory.RequestUris, x => x.EndsWith("/v1/items?page=1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(httpClientFactory.RequestUris, x => x.EndsWith("/v1/items/data/item-abc123", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(httpClientFactory.RequestUris, x => x.EndsWith("/v1/items/data/item-done123", StringComparison.OrdinalIgnoreCase));

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "ABC123");
        Assert.Equal("item-abc123", supplierProduct.SourceSupplierProductId);
        Assert.Contains("https://images.turn14.test/abc123-large.jpg", supplierProduct.SupplierImagesJson);

        var storedGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == globalProduct.Id);
        Assert.Equal(supplierProduct.SupplierImagesJson, storedGlobalProduct.ImagesJson);
        Assert.Contains("Full API description", storedGlobalProduct.LongDescription);
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

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        private readonly RecordingHandler handler = new();

        public IReadOnlyList<string> RequestUris => handler.RequestUris;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<string> requestUris = [];

        public IReadOnlyList<string> RequestUris => requestUris;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            requestUris.Add(uri);
            var content = uri.EndsWith("/v1/token", StringComparison.OrdinalIgnoreCase)
                ? """{"access_token":"turn14-token","expires_in":3600}"""
                : uri.Contains("/v1/items?page=1", StringComparison.OrdinalIgnoreCase)
                    ? ItemsJson()
                : ItemDataJson();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }

        private static string ItemDataJson()
        {
            return """
{
  "data": {
    "id": "item-abc123",
    "attributes": {
      "files": [
        {
          "type": "Image",
          "media_content": "Primary Product Image",
          "file_extension": "jpg",
          "links": [
            { "url": "https://images.turn14.test/abc123-small.jpg", "width": 400, "height": 300, "size": "S" },
            { "url": "https://images.turn14.test/abc123-large.jpg", "width": 1200, "height": 900, "size": "L" }
          ]
        }
      ],
      "descriptions": [
        { "type": "Market Description", "description": "Full API description" }
      ]
    }
  }
}
""";
        }

        private static string ItemsJson()
        {
            return """
{
  "data": [
    {
      "id": "item-abc123",
      "attributes": {
        "part_number": "ABC123"
      }
    }
  ],
  "meta": { "total_pages": 1 }
}
""";
        }
    }
}
