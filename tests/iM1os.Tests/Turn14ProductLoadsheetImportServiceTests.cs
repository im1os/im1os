using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iM1os.Tests;

public sealed class Turn14ProductLoadsheetImportServiceTests
{
    [Fact]
    public async Task Import_uses_spaced_export_headers_and_barcode_without_api_calls()
    {
        var now = new DateTimeOffset(2026, 6, 30, 14, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext(now);
        await using var webExportServer = new Turn14WebExportServer(ProductLoadsheetZip());
        var supplier = new Supplier { Name = "Turn 14 Distribution", Code = "TURN14", ConnectorKey = "TURN14", IsActive = true };
        dbContext.Suppliers.Add(supplier);
        var configuration = new SupplierConnectorConfiguration
        {
            SupplierId = supplier.Id,
            ConnectorKey = "TURN14",
            DisplayName = "Turn14",
            BaseApiUrl = webExportServer.BaseUrl,
            Username = "turn14-user",
            ApiKey = "turn14-client",
            ApiSecretProtected = """{"webPassword":"turn14-password","apiClientSecret":"turn14-secret"}""",
            AuthMode = "WebLogin",
            IsEnabled = true
        };
        dbContext.SupplierConnectorConfigurations.Add(configuration);
        var importRun = new SupplierConnectorImportRun
        {
            SupplierConnectorConfigurationId = configuration.Id,
            ImportType = "Turn14ProductLoadsheet",
            Status = "Queued",
            RequestedAtUtc = now,
            ParametersJson = """{"MaxItems":1}"""
        };
        dbContext.SupplierConnectorImportRuns.Add(importRun);
        await dbContext.SaveChangesAsync();

        var service = new Turn14ProductLoadsheetImportService(
            dbContext,
            new TestClock(now),
            NullLogger<Turn14ProductLoadsheetImportService>.Instance);

        var result = await service.ImportAsync(new Turn14ProductLoadsheetImportRequest(importRun.Id, 1), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Contains("Mozilla/5.0", webExportServer.LastLoginUserAgent);

        var supplierProduct = await dbContext.SupplierProducts.SingleAsync();
        Assert.Equal("ABC123", supplierProduct.SupplierSku);
        Assert.Equal("123", supplierProduct.ManufacturerPartNumber);
        Assert.Equal("123", supplierProduct.NormalizedManufacturerPartNumber);
        Assert.Null(supplierProduct.SourceSupplierProductId);
        Assert.Null(supplierProduct.SupplierImagesJson);

        var globalProduct = await dbContext.GlobalProducts.SingleAsync();
        Assert.Equal("123", globalProduct.ManufacturerPartNumber);
        Assert.Equal("123", globalProduct.NormalizedManufacturerPartNumber);
        Assert.Equal("123456789012", globalProduct.Upc);
        Assert.Null(globalProduct.ImagesJson);
    }

    private static byte[] ProductLoadsheetZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("turn14-loadsheet.csv");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.WriteLine("Primary Vendor,Internal Part Number,Product Category,Product Name,Description,Cost,Retail,Map,Total Inventory,Central Stock,East Stock,Midwest Stock,West Stock,ETA,Case Quantity,Barcode,Part Number Prefix,Part Number,Alternate Part Number,Pricing Group,Product Department");
            writer.WriteLine("ACME,ABC123,Exhaust,Slip-On Exhaust,Loadsheet description,10.00,20.00,18.00,5,1,2,1,1,,1,123456789012,AC,123,ALT123,PG1,Powersports");
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

    private sealed class Turn14WebExportServer : IAsyncDisposable
    {
        private readonly byte[] zipContent;
        private readonly CancellationTokenSource stop = new();
        private readonly TcpListener listener;
        private readonly Task acceptLoop;

        public Turn14WebExportServer(byte[] zipContent)
        {
            this.zipContent = zipContent;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
            acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public string BaseUrl { get; }

        public string LastLoginUserAgent { get; private set; } = string.Empty;

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            listener.Stop();
            try
            {
                await acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }

            stop.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                _ = Task.Run(() => HandleClientAsync(client), stop.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using var clientScope = client;
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync();
            var headers = new List<string>();
            string? header;
            while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync()))
            {
                headers.Add(header);
            }

            var path = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) ?? "/";
            if (path.Equals("/user/login", StringComparison.OrdinalIgnoreCase))
            {
                LastLoginUserAgent = headers
                    .FirstOrDefault(x => x.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            }

            if (path.Equals("/export.php", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, "application/zip", zipContent, "Content-Disposition: attachment; filename=\"turn14-loadsheet.zip\"\r\n");
                return;
            }

            var html = path.Equals("/user/login", StringComparison.OrdinalIgnoreCase)
                ? "<html><a href=\"/user/logout\">LOGOUT</a></html>"
                : "<html>Turn14</html>";
            await WriteResponseAsync(stream, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));
        }

        private static async Task WriteResponseAsync(Stream stream, string contentType, byte[] content, string extraHeaders = "")
        {
            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {content.Length}\r\n" +
                extraHeaders +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(headers);
            await stream.WriteAsync(content);
        }
    }
}
