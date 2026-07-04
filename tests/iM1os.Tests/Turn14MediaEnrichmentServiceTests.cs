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

        var httpClientFactory = new RecordingHttpClientFactory(itemsJson: ItemsJsonWithThumbnail());
        var service = new Turn14MediaEnrichmentService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<Turn14MediaEnrichmentService>.Instance);

        var result = await service.ImportAsync(new Turn14MediaEnrichmentRunRequest(importRun.Id, 10, 0), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.UpdatedProducts);
        Assert.Contains(httpClientFactory.RequestUris, x => x.EndsWith("/v1/items?page=1", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(httpClientFactory.RequestUris, x => x.Contains("/v1/items/data/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(httpClientFactory.RequestUris, x => x.EndsWith("/v1/items/data/item-done123", StringComparison.OrdinalIgnoreCase));

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "ABC123");
        Assert.Equal("item-abc123", supplierProduct.SourceSupplierProductId);
        Assert.Contains("https://images.turn14.test/abc123-thumb.jpg", supplierProduct.SupplierImagesJson);

        var storedGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == globalProduct.Id);
        Assert.Equal(supplierProduct.SupplierImagesJson, storedGlobalProduct.ImagesJson);
    }

    [Fact]
    public async Task Import_sanitizes_null_characters_from_turn14_thumbnail_json()
    {
        var now = new DateTimeOffset(2026, 7, 2, 20, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14", IsActive = true };
        var globalProduct = new GlobalProduct { Brand = "ACME", Description = "Intake", Status = "Active" };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(globalProduct);
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
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = globalProduct.Id,
            SupplierSku = "ABC123",
            SupplierStatus = "Active"
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

        var httpClientFactory = new RecordingHttpClientFactory(itemsJson: ItemsJsonWithNullCharacterThumbnail());
        var service = new Turn14MediaEnrichmentService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<Turn14MediaEnrichmentService>.Instance);

        await service.ImportAsync(new Turn14MediaEnrichmentRunRequest(importRun.Id, 10, 0), CancellationToken.None);

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "ABC123");
        var storedGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == globalProduct.Id);
        Assert.DoesNotContain("\\u0000", supplierProduct.SupplierImagesJson);
        Assert.DoesNotContain('\0', supplierProduct.SupplierImagesJson ?? string.Empty);
        Assert.Contains("https://images.turn14.test/abc123-thumb.jpg", supplierProduct.SupplierImagesJson);
        Assert.Equal(supplierProduct.SupplierImagesJson, storedGlobalProduct.ImagesJson);
    }

    [Fact]
    public async Task Import_bulk_fills_thumbnail_from_turn14_items_index_without_item_data_call()
    {
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14", IsActive = true };
        var globalProduct = new GlobalProduct { Brand = "ACME", Description = "Intake", Status = "Active" };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(globalProduct);
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
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = globalProduct.Id,
            SupplierSku = "ABC123",
            SupplierStatus = "Active"
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

        var httpClientFactory = new RecordingHttpClientFactory(itemsJson: ItemsJsonWithThumbnail());
        var service = new Turn14MediaEnrichmentService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<Turn14MediaEnrichmentService>.Instance);

        var result = await service.ImportAsync(new Turn14MediaEnrichmentRunRequest(importRun.Id, 10, 0), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.UpdatedProducts);
        Assert.Contains(httpClientFactory.RequestUris, x => x.EndsWith("/v1/items?page=1", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(httpClientFactory.RequestUris, x => x.Contains("/v1/items/data/", StringComparison.OrdinalIgnoreCase));

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "ABC123");
        Assert.Equal("item-abc123", supplierProduct.SourceSupplierProductId);
        Assert.Contains("https://images.turn14.test/abc123-thumb.jpg", supplierProduct.SupplierImagesJson);

        var storedGlobalProduct = await dbContext.GlobalProducts.SingleAsync(x => x.Id == globalProduct.Id);
        Assert.Equal(supplierProduct.SupplierImagesJson, storedGlobalProduct.ImagesJson);
    }

    [Fact]
    public async Task Import_refreshes_turn14_token_and_retries_after_unauthorized_response()
    {
        var now = new DateTimeOffset(2026, 7, 3, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14", IsActive = true };
        var globalProduct = new GlobalProduct { Brand = "ACME", Description = "Intake", Status = "Active" };
        dbContext.Suppliers.Add(supplier);
        dbContext.GlobalProducts.Add(globalProduct);
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
        dbContext.SupplierProducts.Add(new SupplierProduct
        {
            SupplierId = supplier.Id,
            GlobalProductId = globalProduct.Id,
            SupplierSku = "ABC123",
            SourceSupplierProductId = "item-abc123",
            SupplierStatus = "Active"
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

        var httpClientFactory = new RecordingHttpClientFactory(unauthorizedItemsOnce: true, itemsJson: ItemsJsonWithThumbnail());
        var service = new Turn14MediaEnrichmentService(
            dbContext,
            httpClientFactory,
            new TestClock(now),
            NullLogger<Turn14MediaEnrichmentService>.Instance);

        var result = await service.ImportAsync(new Turn14MediaEnrichmentRunRequest(importRun.Id, 10, 0), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.UpdatedProducts);
        Assert.Equal(2, httpClientFactory.RequestUris.Count(x => x.EndsWith("/v1/token", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, httpClientFactory.RequestUris.Count(x => x.EndsWith("/v1/items?page=1", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(httpClientFactory.RequestUris, x => x.Contains("/v1/items/data/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Bearer turn14-token-1", httpClientFactory.AuthorizationHeaders);
        Assert.Contains("Bearer turn14-token-2", httpClientFactory.AuthorizationHeaders);

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync(x => x.SupplierSku == "ABC123");
        Assert.Contains("https://images.turn14.test/abc123-thumb.jpg", supplierProduct.SupplierImagesJson);
    }

    private static ApplicationDbContext CreateContext(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(now), new TenantProvider(currentUser));
    }

    private static string ItemsJsonWithNullCharacterThumbnail()
    {
        return """
{
  "data": [
    {
      "id": "item-abc123",
      "attributes": {
        "part_number": "ABC123",
        "thumbnail": "https://images.turn14.test/abc123-thumb.jpg\u0000"
      }
    }
  ],
  "meta": { "total_pages": 1 }
}
""";
    }

    private sealed class TestClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;
    }

    private static string ItemsJsonWithThumbnail()
    {
        return """
{
  "data": [
    {
      "id": "item-abc123",
      "attributes": {
        "part_number": "ABC123",
        "thumbnail": "https://images.turn14.test/abc123-thumb.jpg"
      }
    }
  ],
  "meta": { "total_pages": 1 }
}
""";
    }

    private sealed class RecordingHttpClientFactory(
        string? itemDataJson = null,
        bool unauthorizedItemDataOnce = false,
        string? itemsJson = null,
        bool unauthorizedItemsOnce = false) : IHttpClientFactory
    {
        private readonly RecordingHandler handler = new(itemDataJson, unauthorizedItemDataOnce, itemsJson, unauthorizedItemsOnce);

        public IReadOnlyList<string> RequestUris => handler.RequestUris;
        public IReadOnlyList<string?> AuthorizationHeaders => handler.AuthorizationHeaders;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHandler(string? itemDataJson, bool unauthorizedItemDataOnce, string? itemsJson, bool unauthorizedItemsOnce) : HttpMessageHandler
    {
        private readonly List<string> requestUris = [];
        private readonly List<string?> authorizationHeaders = [];
        private int tokenRequestCount;
        private int itemDataRequestCount;
        private int itemsRequestCount;

        public IReadOnlyList<string> RequestUris => requestUris;
        public IReadOnlyList<string?> AuthorizationHeaders => authorizationHeaders;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            requestUris.Add(uri);
            authorizationHeaders.Add(request.Headers.Authorization is null
                ? null
                : $"{request.Headers.Authorization.Scheme} {request.Headers.Authorization.Parameter}");
            if (uri.EndsWith("/v1/token", StringComparison.OrdinalIgnoreCase))
            {
                tokenRequestCount++;
                return JsonResponse($$"""{"access_token":"turn14-token-{{tokenRequestCount}}","expires_in":3600}""");
            }

            if (uri.Contains("/v1/items?page=1", StringComparison.OrdinalIgnoreCase))
            {
                itemsRequestCount++;
                if (unauthorizedItemsOnce && itemsRequestCount == 1)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
                }

                return JsonResponse(itemsJson ?? ItemsJson());
            }

            itemDataRequestCount++;
            if (unauthorizedItemDataOnce && itemDataRequestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            return JsonResponse(itemDataJson ?? ItemDataJson());
        }

        private static Task<HttpResponseMessage> JsonResponse(string content)
        {
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
