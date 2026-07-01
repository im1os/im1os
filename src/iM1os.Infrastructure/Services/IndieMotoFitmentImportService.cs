using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Services;

public sealed class IndieMotoFitmentImportService(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<IndieMotoFitmentImportService> logger) : IIndieMotoFitmentImportService
{
    private const string DefaultBaseUrl = "https://saas.indie-moto.com";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IndieMotoFitmentImportResult> ImportAsync(IndieMotoFitmentImportRequest request, CancellationToken cancellationToken)
    {
        var supplierCode = Required(request.SupplierCode, "Supplier code");
        var requestedSku = Clean(request.Sku);
        var fitmentLimit = request.FitmentLimit is > 0 ? request.FitmentLimit : null;
        var delayMilliseconds = Math.Max(0, request.DelayMilliseconds);
        var baseUrl = Clean(request.BaseUrl) ?? DefaultBaseUrl;
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Code == supplierCode, cancellationToken);
        var supplierProductsQuery = dbContext.SupplierProducts
            .AsNoTracking()
            .Where(x => x.SupplierId == supplier.Id);
        if (requestedSku is not null)
        {
            supplierProductsQuery = supplierProductsQuery.Where(x => x.SupplierSku == requestedSku);
        }
        else if (request.ExcludeNla)
        {
            supplierProductsQuery = supplierProductsQuery.Where(x => x.SupplierStatus != "NLA");
        }

        var supplierProducts = await supplierProductsQuery
            .OrderBy(x => x.SupplierSku)
            .Take(request.MaxSkus ?? int.MaxValue)
            .Select(x => new SupplierProductImportRow(
                x.Id,
                x.GlobalProductId,
                x.SupplierSku,
                x.SourceSupplierProductId))
            .ToListAsync(cancellationToken);

        var client = httpClientFactory.CreateClient("IndieMotoFitment");
        var counters = new ImportCounters();
        SupplierConnectorImportRun? importRun = null;
        if (request.ImportRunId is not null)
        {
            importRun = await dbContext.SupplierConnectorImportRuns
                .SingleOrDefaultAsync(x => x.Id == request.ImportRunId.Value, cancellationToken);
            if (importRun is not null)
            {
                importRun.ProgressProcessed = 0;
                importRun.ProgressTotal = supplierProducts.Count;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        foreach (var supplierProduct in supplierProducts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lookup = await GetFitmentRowsAsync(client, baseUrl, supplierProduct.SupplierSku, fitmentLimit, cancellationToken);
                counters.SkusProcessed++;
                if (lookup.Rows.Count > 0)
                {
                    counters.SkusWithFitment++;
                }
                else if (lookup.PartsUnlimitedQueued)
                {
                    counters.SkusQueuedForPartsUnlimitedCrawl++;
                }
                else
                {
                    counters.SkusWithoutFitment++;
                }

                foreach (var row in lookup.Rows)
                {
                    await UpsertFitmentAsync(supplier.Id, supplierProduct, row, counters, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Imported {FitmentRows} fitment rows for {SupplierCode} SKU {SupplierSku}. QueuedForPartsUnlimitedCrawl={QueuedForPartsUnlimitedCrawl}, QueueReason={QueueReason}.",
                    lookup.Rows.Count,
                    supplierCode,
                    supplierProduct.SupplierSku,
                    lookup.PartsUnlimitedQueued,
                    lookup.PartsUnlimitedQueueReason);
            }
            catch (Exception exception)
            {
                counters.FailedSkus++;
                logger.LogWarning(exception, "Failed to import fitment for {SupplierCode} SKU {SupplierSku}.", supplierCode, supplierProduct.SupplierSku);
            }

            if (importRun is not null)
            {
                importRun.ProgressProcessed = counters.SkusProcessed + counters.FailedSkus;
                importRun.ProgressTotal = supplierProducts.Count;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
        }

        return new IndieMotoFitmentImportResult(
            counters.SkusProcessed,
            counters.SkusWithFitment,
            counters.SkusQueuedForPartsUnlimitedCrawl,
            counters.SkusWithoutFitment,
            counters.FitmentRowsProcessed,
            counters.SourceFitmentRowsUpserted,
            counters.GlobalVehiclesUpserted,
            counters.VehicleFitmentsUpserted,
            counters.FailedSkus);
    }

    private static async Task<FitmentLookupResult> GetFitmentRowsAsync(HttpClient client, string baseUrl, string sku, int? fitmentLimit, CancellationToken cancellationToken)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/ymm?intent=sku&sku={Uri.EscapeDataString(sku)}";
        if (fitmentLimit is not null)
        {
            url += $"&limit={fitmentLimit.Value}";
        }

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var queue = ParsePartsUnlimitedQueue(root);
        if (!root.TryGetProperty("fitment", out var fitment) || fitment.ValueKind != JsonValueKind.Array)
        {
            return new FitmentLookupResult([], queue.Queued, queue.Reason);
        }

        var rows = new List<IndieMotoFitmentRow>();
        foreach (var item in fitment.EnumerateArray())
        {
            var year = IntValue(item, "year");
            var make = Clean(StringValue(item, "make"));
            var model = Clean(StringValue(item, "model"));
            var supplierKey = Clean(StringValue(item, "supplierKey"));
            var supplierSku = Clean(StringValue(item, "sku"));
            var vehicleClass = Clean(StringValue(item, "vehicleClass"));
            var vehicleType = Clean(StringValue(item, "vehicleClassLabel")) ?? vehicleClass;
            if (year is null || make is null || model is null || supplierKey is null || supplierSku is null)
            {
                continue;
            }

            rows.Add(new IndieMotoFitmentRow(
                supplierKey,
                StringValue(item, "supplierProductId"),
                StringValue(item, "supplierPartNumber"),
                supplierSku,
                StringValue(item, "fitmentItemId"),
                StringValue(item, "fitmentPartNumber"),
                StringValue(item, "mfgPartNumber"),
                vehicleClass,
                vehicleType,
                year.Value,
                make,
                model,
                Clean(StringValue(item, "submodel")),
                Clean(StringValue(item, "engine")),
                Clean(StringValue(item, "notes"))));
        }

        return new FitmentLookupResult(rows, queue.Queued, queue.Reason);
    }

    private async Task UpsertFitmentAsync(
        Guid supplierId,
        SupplierProductImportRow supplierProduct,
        IndieMotoFitmentRow row,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var globalVehicle = await dbContext.GlobalVehicles
            .FirstOrDefaultAsync(x =>
                x.Year == row.Year &&
                x.Make == row.Make &&
                x.Model == row.Model &&
                x.Submodel == row.Submodel &&
                x.Engine == row.Engine &&
                x.Market == "US",
                cancellationToken);

        if (globalVehicle is null)
        {
            globalVehicle = new GlobalVehicle
            {
                Year = row.Year,
                Make = row.Make,
                Model = row.Model,
                VehicleClass = row.VehicleClass,
                VehicleType = row.VehicleType,
                Submodel = row.Submodel,
                Engine = row.Engine,
                Market = "US"
            };
            dbContext.GlobalVehicles.Add(globalVehicle);
            counters.GlobalVehiclesUpserted++;
        }
        else
        {
            globalVehicle.VehicleClass ??= row.VehicleClass;
            globalVehicle.VehicleType ??= row.VehicleType;
        }

        var vehicleFitment = await dbContext.VehicleFitments
            .FirstOrDefaultAsync(x =>
                x.GlobalProductId == supplierProduct.GlobalProductId &&
                x.GlobalVehicleId == globalVehicle.Id &&
                x.Position == null,
                cancellationToken);

        if (vehicleFitment is null)
        {
            vehicleFitment = new VehicleFitment
            {
                GlobalProductId = supplierProduct.GlobalProductId,
                GlobalVehicleId = globalVehicle.Id,
                Quantity = 1,
                Notes = row.Notes
            };
            dbContext.VehicleFitments.Add(vehicleFitment);
            counters.VehicleFitmentsUpserted++;
        }
        else if (string.IsNullOrWhiteSpace(vehicleFitment.Notes) && !string.IsNullOrWhiteSpace(row.Notes))
        {
            vehicleFitment.Notes = row.Notes;
        }

        var sourceRecord = await dbContext.SupplierFitmentRecords
            .FirstOrDefaultAsync(x =>
                x.SupplierId == supplierId &&
                x.SupplierSku == row.SupplierSku &&
                x.SourceFitmentItemId == row.SourceFitmentItemId &&
                x.Year == row.Year &&
                x.Make == row.Make &&
                x.Model == row.Model &&
                x.VehicleClass == row.VehicleClass &&
                x.Submodel == row.Submodel &&
                x.Engine == row.Engine,
                cancellationToken);

        if (sourceRecord is null)
        {
            sourceRecord = new SupplierFitmentRecord
            {
                SupplierId = supplierId,
                SupplierKey = row.SupplierKey,
                SupplierSku = row.SupplierSku,
                Year = row.Year,
                Make = row.Make,
                Model = row.Model,
                ResolutionStatus = "Resolved",
                ImportedAtUtc = now
            };
            dbContext.SupplierFitmentRecords.Add(sourceRecord);
            counters.SourceFitmentRowsUpserted++;
        }

        sourceRecord.SupplierProductId = supplierProduct.Id;
        sourceRecord.GlobalProductId = supplierProduct.GlobalProductId;
        sourceRecord.GlobalVehicleId = globalVehicle.Id;
        sourceRecord.VehicleFitmentId = vehicleFitment.Id;
        sourceRecord.SupplierKey = row.SupplierKey;
        sourceRecord.SourceSupplierProductId = Clean(row.SourceSupplierProductId);
        sourceRecord.SupplierPartNumber = Clean(row.SupplierPartNumber);
        sourceRecord.SupplierSku = row.SupplierSku;
        sourceRecord.SourceFitmentItemId = Clean(row.SourceFitmentItemId);
        sourceRecord.SourceFitmentPartNumber = Clean(row.SourceFitmentPartNumber);
        sourceRecord.MfgPartNumber = Clean(row.MfgPartNumber);
        sourceRecord.VehicleClass = row.VehicleClass;
        sourceRecord.VehicleType = row.VehicleType;
        sourceRecord.Year = row.Year;
        sourceRecord.Make = row.Make;
        sourceRecord.Model = row.Model;
        sourceRecord.Submodel = row.Submodel;
        sourceRecord.Engine = row.Engine;
        sourceRecord.Notes = row.Notes;
        sourceRecord.ResolutionStatus = "Resolved";
        sourceRecord.ImportedAtUtc = now;

        var sourceSupplierProductId = Clean(row.SourceSupplierProductId);
        if (!string.IsNullOrWhiteSpace(sourceSupplierProductId) &&
            string.IsNullOrWhiteSpace(supplierProduct.SourceSupplierProductId))
        {
            var storedSupplierProduct = await dbContext.SupplierProducts
                .SingleAsync(x => x.Id == supplierProduct.Id, cancellationToken);
            storedSupplierProduct.SourceSupplierProductId = sourceSupplierProductId;
        }

        counters.FitmentRowsProcessed++;
    }

    private static string? StringValue(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
    }

    private static int? IntValue(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static PartsUnlimitedQueueInfo ParsePartsUnlimitedQueue(JsonElement root)
    {
        if (!root.TryGetProperty("partsUnlimitedQueue", out var queue) || queue.ValueKind != JsonValueKind.Object)
        {
            return new PartsUnlimitedQueueInfo(false, null);
        }

        var queued = queue.TryGetProperty("queued", out var queuedElement) && queuedElement.ValueKind == JsonValueKind.True;
        return new PartsUnlimitedQueueInfo(queued, Clean(StringValue(queue, "reason")));
    }

    private static string Required(string? value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.")
            : value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record SupplierProductImportRow(Guid Id, Guid GlobalProductId, string SupplierSku, string? SourceSupplierProductId);

    private sealed record FitmentLookupResult(
        IReadOnlyCollection<IndieMotoFitmentRow> Rows,
        bool PartsUnlimitedQueued,
        string? PartsUnlimitedQueueReason);

    private sealed record PartsUnlimitedQueueInfo(bool Queued, string? Reason);

    private sealed record IndieMotoFitmentRow(
        string SupplierKey,
        string? SourceSupplierProductId,
        string? SupplierPartNumber,
        string SupplierSku,
        string? SourceFitmentItemId,
        string? SourceFitmentPartNumber,
        string? MfgPartNumber,
        string? VehicleClass,
        string? VehicleType,
        int Year,
        string Make,
        string Model,
        string? Submodel,
        string? Engine,
        string? Notes);

    private sealed class ImportCounters
    {
        public int SkusProcessed { get; set; }
        public int SkusWithFitment { get; set; }
        public int SkusQueuedForPartsUnlimitedCrawl { get; set; }
        public int SkusWithoutFitment { get; set; }
        public int FitmentRowsProcessed { get; set; }
        public int SourceFitmentRowsUpserted { get; set; }
        public int GlobalVehiclesUpserted { get; set; }
        public int VehicleFitmentsUpserted { get; set; }
        public int FailedSkus { get; set; }
    }
}
