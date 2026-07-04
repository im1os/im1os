using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const int MaxBatchSize = 500;
    private const string DefaultBaseUrl = "https://saas.indie-moto.com";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

        foreach (var batch in supplierProducts.Chunk(MaxBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (batch.Length == 1)
            {
                var supplierProduct = batch[0];
                try
                {
                    var lookup = await GetFitmentRowsAsync(client, baseUrl, supplierCode, supplierProduct.SupplierSku, fitmentLimit, cancellationToken);
                    await ImportLookupAsync(supplier.Id, supplierCode, supplierProduct, FilterLookupRows(supplierCode, supplierProduct, lookup), counters, cancellationToken);

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
            }
            else
            {
                IReadOnlyDictionary<string, FitmentLookupResult> batchLookups;
                try
                {
                    batchLookups = await GetBatchFitmentRowsAsync(
                        client,
                        baseUrl,
                        supplierCode,
                        batch.Select(x => x.SupplierSku).ToArray(),
                        fitmentLimit,
                        cancellationToken);
                }
                catch (BatchFitmentLookupUnsupportedException exception)
                {
                    logger.LogWarning(exception, "Batch fitment lookup is not supported by {BaseUrl}; falling back to single-SKU GET lookups for {SupplierCode}. BatchSize={BatchSize}.", baseUrl, supplierCode, batch.Length);
                    foreach (var supplierProduct in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var lookup = await GetFitmentRowsAsync(client, baseUrl, supplierCode, supplierProduct.SupplierSku, fitmentLimit, cancellationToken);
                            await ImportLookupAsync(supplier.Id, supplierCode, supplierProduct, FilterLookupRows(supplierCode, supplierProduct, lookup), counters, cancellationToken);
                        }
                        catch (Exception singleSkuException)
                        {
                            counters.FailedSkus++;
                            logger.LogWarning(singleSkuException, "Failed to import fitment fallback for {SupplierCode} SKU {SupplierSku}.", supplierCode, supplierProduct.SupplierSku);
                        }

                        if (delayMilliseconds > 0)
                        {
                            await Task.Delay(delayMilliseconds, cancellationToken);
                        }
                    }

                    batchLookups = new Dictionary<string, FitmentLookupResult>(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception exception)
                {
                    counters.FailedSkus += batch.Length;
                    logger.LogWarning(exception, "Failed to import batch fitment for {SupplierCode}. BatchSize={BatchSize}.", supplierCode, batch.Length);
                    batchLookups = new Dictionary<string, FitmentLookupResult>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var supplierProduct in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (batchLookups.TryGetValue(supplierProduct.SupplierSku, out var lookup))
                    {
                        continue;
                    }

                    if (batchLookups.Count > 0)
                    {
                        counters.SkusProcessed++;
                        counters.SkusWithoutFitment++;
                    }
                }

                if (batchLookups.Count > 0)
                {
                    await ImportBatchLookupsAsync(
                        supplier.Id,
                        supplierCode,
                        batch
                            .Where(x => batchLookups.ContainsKey(x.SupplierSku))
                            .Select(x => new PendingFitmentLookup(x, FilterLookupRows(supplierCode, x, batchLookups[x.SupplierSku])))
                            .ToList(),
                        counters,
                        cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Imported batch fitment for {SupplierCode}. BatchSize={BatchSize}, ImportedSkus={ImportedSkus}.",
                    supplierCode,
                    batch.Length,
                    batchLookups.Count);
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

    private async Task ImportBatchLookupsAsync(
        Guid supplierId,
        string supplierCode,
        IReadOnlyCollection<PendingFitmentLookup> lookups,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var rows = lookups
            .SelectMany(x => x.Lookup.Rows.Select(row => new PendingFitmentRow(x.SupplierProduct, row)))
            .ToList();
        var context = await BatchFitmentUpsertContext.LoadAsync(dbContext, supplierId, rows, cancellationToken);

        foreach (var item in lookups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            counters.SkusProcessed++;
            if (item.Lookup.Rows.Count > 0)
            {
                counters.SkusWithFitment++;
            }
            else if (item.Lookup.PartsUnlimitedQueued)
            {
                counters.SkusQueuedForPartsUnlimitedCrawl++;
            }
            else
            {
                counters.SkusWithoutFitment++;
            }

            foreach (var row in item.Lookup.Rows)
            {
                UpsertFitment(context, supplierId, item.SupplierProduct, row, counters);
            }

            logger.LogDebug(
                "Prepared {FitmentRows} fitment rows for {SupplierCode} SKU {SupplierSku}. QueuedForPartsUnlimitedCrawl={QueuedForPartsUnlimitedCrawl}, QueueReason={QueueReason}.",
                item.Lookup.Rows.Count,
                supplierCode,
                item.SupplierProduct.SupplierSku,
                item.Lookup.PartsUnlimitedQueued,
                item.Lookup.PartsUnlimitedQueueReason);
        }
    }

    private async Task ImportLookupAsync(
        Guid supplierId,
        string supplierCode,
        SupplierProductImportRow supplierProduct,
        FitmentLookupResult lookup,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
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

        var context = await BatchFitmentUpsertContext.LoadAsync(
            dbContext,
            supplierId,
            lookup.Rows.Select(row => new PendingFitmentRow(supplierProduct, row)).ToList(),
            cancellationToken);
        foreach (var row in lookup.Rows)
        {
            UpsertFitment(context, supplierId, supplierProduct, row, counters);
        }

        logger.LogDebug(
            "Prepared {FitmentRows} fitment rows for {SupplierCode} SKU {SupplierSku}. QueuedForPartsUnlimitedCrawl={QueuedForPartsUnlimitedCrawl}, QueueReason={QueueReason}.",
            lookup.Rows.Count,
            supplierCode,
            supplierProduct.SupplierSku,
            lookup.PartsUnlimitedQueued,
            lookup.PartsUnlimitedQueueReason);
    }

    private void UpsertFitment(
        BatchFitmentUpsertContext context,
        Guid supplierId,
        SupplierProductImportRow supplierProduct,
        IndieMotoFitmentRow row,
        ImportCounters counters)
    {
        var now = dateTimeProvider.UtcNow;
        var vehicleKey = VehicleKey.From(row);
        if (!context.GlobalVehiclesByKey.TryGetValue(vehicleKey, out var globalVehicle))
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
                Market = vehicleKey.Market
            };
            dbContext.GlobalVehicles.Add(globalVehicle);
            context.GlobalVehiclesByKey[vehicleKey] = globalVehicle;
            counters.GlobalVehiclesUpserted++;
        }
        else
        {
            globalVehicle.VehicleClass ??= row.VehicleClass;
            globalVehicle.VehicleType ??= row.VehicleType;
        }

        var vehicleFitmentKey = new VehicleFitmentKey(supplierProduct.GlobalProductId, globalVehicle.Id, null);
        if (!context.VehicleFitmentsByKey.TryGetValue(vehicleFitmentKey, out var vehicleFitment))
        {
            vehicleFitment = new VehicleFitment
            {
                GlobalProductId = supplierProduct.GlobalProductId,
                GlobalVehicleId = globalVehicle.Id,
                Quantity = 1,
                Notes = row.Notes
            };
            dbContext.VehicleFitments.Add(vehicleFitment);
            context.VehicleFitmentsByKey[vehicleFitmentKey] = vehicleFitment;
            counters.VehicleFitmentsUpserted++;
        }
        else if (string.IsNullOrWhiteSpace(vehicleFitment.Notes) && !string.IsNullOrWhiteSpace(row.Notes))
        {
            vehicleFitment.Notes = row.Notes;
        }

        var sourceRecordKey = SourceFitmentRecordKey.From(supplierId, row);
        if (!context.SupplierFitmentRecordsByKey.TryGetValue(sourceRecordKey, out var sourceRecord))
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
            context.SupplierFitmentRecordsByKey[sourceRecordKey] = sourceRecord;
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
            string.IsNullOrWhiteSpace(supplierProduct.SourceSupplierProductId) &&
            context.SupplierProductsById.TryGetValue(supplierProduct.Id, out var storedSupplierProduct) &&
            string.IsNullOrWhiteSpace(storedSupplierProduct.SourceSupplierProductId))
        {
            storedSupplierProduct.SourceSupplierProductId = sourceSupplierProductId;
        }

        counters.FitmentRowsProcessed++;
    }

    private static async Task<FitmentLookupResult> GetFitmentRowsAsync(HttpClient client, string baseUrl, string supplierCode, string sku, int? fitmentLimit, CancellationToken cancellationToken)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/ymm?intent=sku&sku={Uri.EscapeDataString(sku)}";
        var ymmSupplier = YmmSupplierValue(supplierCode);
        if (ymmSupplier is not null)
        {
            url += $"&supplier={Uri.EscapeDataString(ymmSupplier)}";
        }

        if (IsPartsUnlimitedSupplier(supplierCode))
        {
            url += "&queuePartsUnlimited=true";
        }

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

        return new FitmentLookupResult(ParseFitmentRows(fitment), queue.Queued, queue.Reason);
    }

    private static async Task<IReadOnlyDictionary<string, FitmentLookupResult>> GetBatchFitmentRowsAsync(HttpClient client, string baseUrl, string supplierCode, IReadOnlyCollection<string> skus, int? fitmentLimit, CancellationToken cancellationToken)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/ymm";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var ymmSupplier = YmmSupplierValue(supplierCode);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new BatchFitmentRequest(
                "fitment-batch",
                skus,
                fitmentLimit,
                ymmSupplier,
                IsPartsUnlimitedSupplier(supplierCode) ? true : null), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest &&
                body.Contains("Unknown YMM API action", StringComparison.OrdinalIgnoreCase))
            {
                throw new BatchFitmentLookupUnsupportedException(TrimForMessage(body));
            }

            throw new InvalidOperationException($"Batch fitment lookup failed with HTTP {(int)response.StatusCode}: {TrimForMessage(body)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseBatchFitmentLookups(document.RootElement, skus);
    }

    private static IReadOnlyDictionary<string, FitmentLookupResult> ParseBatchFitmentLookups(JsonElement root, IReadOnlyCollection<string> requestedSkus)
    {
        var lookups = new Dictionary<string, FitmentLookupResult>(StringComparer.OrdinalIgnoreCase);
        var queueResults = ParsePartsUnlimitedQueueResults(root);

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var sku = Clean(StringValue(item, "sku")) ?? Clean(StringValue(item, "partNumber"));
                if (sku is null)
                {
                    continue;
                }

                var queue = item.TryGetProperty("partsUnlimitedQueue", out _)
                    ? ParsePartsUnlimitedQueue(item)
                    : queueResults.GetValueOrDefault(sku, new PartsUnlimitedQueueInfo(false, null));
                lookups[sku] = new FitmentLookupResult(
                    item.TryGetProperty("fitment", out var fitment) && fitment.ValueKind == JsonValueKind.Array
                        ? ParseFitmentRows(fitment)
                        : [],
                    queue.Queued,
                    queue.Reason);
            }
        }

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Object)
        {
            foreach (var result in results.EnumerateObject())
            {
                if (result.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                if (lookups.ContainsKey(result.Name))
                {
                    continue;
                }

                var queue = queueResults.GetValueOrDefault(result.Name, new PartsUnlimitedQueueInfo(false, null));
                lookups[result.Name] = new FitmentLookupResult(
                    ParseFitmentRows(result.Value),
                    queue.Queued,
                    queue.Reason);
            }
        }

        foreach (var sku in requestedSkus)
        {
            var queue = queueResults.GetValueOrDefault(sku, new PartsUnlimitedQueueInfo(false, null));
            lookups.TryAdd(sku, new FitmentLookupResult([], queue.Queued, queue.Reason));
        }

        return lookups;
    }

    private static IReadOnlyCollection<IndieMotoFitmentRow> ParseFitmentRows(JsonElement fitment)
    {
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

        return rows;
    }

    private static FitmentLookupResult FilterLookupRows(string supplierCode, SupplierProductImportRow supplierProduct, FitmentLookupResult lookup)
    {
        if (lookup.Rows.Count == 0)
        {
            return lookup;
        }

        var rows = lookup.Rows
            .Where(row => IsExpectedFitmentRow(supplierCode, supplierProduct, row))
            .ToList();
        return rows.Count == lookup.Rows.Count
            ? lookup
            : lookup with { Rows = rows };
    }

    private static bool IsExpectedFitmentRow(string supplierCode, SupplierProductImportRow supplierProduct, IndieMotoFitmentRow row)
    {
        if (!string.Equals(NormalizeSupplierCode(supplierCode), NormalizeSupplierCode(row.SupplierKey), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestedSku = NormalizeIdentifier(supplierProduct.SupplierSku);
        if (requestedSku is null)
        {
            return false;
        }

        return FitmentRowIdentifiers(row)
            .Select(NormalizeIdentifier)
            .Any(identifier => string.Equals(identifier, requestedSku, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string?> FitmentRowIdentifiers(IndieMotoFitmentRow row)
    {
        yield return row.SupplierSku;
        yield return row.SupplierPartNumber;
        yield return row.SourceFitmentPartNumber;
        yield return row.SourceFitmentItemId;
        yield return row.SourceSupplierProductId;
    }

    private static string NormalizeSupplierCode(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return string.Empty;
        }

        var normalized = new string(clean.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return normalized switch
        {
            "PU" or "PARTSUNLIMITED" => "PU",
            "WPS" or "WESTERNPOWERSPORTS" => "WPS",
            "TURN14" or "TURN14DISTRIBUTION" => "TURN14",
            _ => normalized
        };
    }

    private static string? NormalizeIdentifier(string? value)
    {
        var clean = Clean(value);
        return clean is null
            ? null
            : new string(clean.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
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

    private static IReadOnlyDictionary<string, PartsUnlimitedQueueInfo> ParsePartsUnlimitedQueueResults(JsonElement root)
    {
        if (!root.TryGetProperty("partsUnlimitedQueue", out var queue) ||
            queue.ValueKind != JsonValueKind.Object ||
            !queue.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, PartsUnlimitedQueueInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var queueResults = new Dictionary<string, PartsUnlimitedQueueInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results.EnumerateArray())
        {
            var partNumber = Clean(StringValue(result, "partNumber")) ??
                Clean(StringValue(result, "sku")) ??
                Clean(StringValue(result, "supplierPartNumber"));
            if (partNumber is null)
            {
                continue;
            }

            var queued = result.TryGetProperty("queued", out var queuedElement) && queuedElement.ValueKind == JsonValueKind.True;
            queueResults[partNumber] = new PartsUnlimitedQueueInfo(queued, Clean(StringValue(result, "reason")));
        }

        return queueResults;
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

    private static string TrimForMessage(string? value)
    {
        var clean = string.IsNullOrWhiteSpace(value)
            ? "No response body returned."
            : value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return clean.Length <= 500 ? clean : $"{clean[..500]}...";
    }

    private static bool IsPartsUnlimitedSupplier(string supplierCode)
    {
        return string.Equals(supplierCode, "PU", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCode, "PARTS_UNLIMITED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCode, "PARTSUNLIMITED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCode, "PARTS-UNLIMITED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCode, "PARTS UNLIMITED", StringComparison.OrdinalIgnoreCase);
    }

    private static string? YmmSupplierValue(string supplierCode)
    {
        if (IsPartsUnlimitedSupplier(supplierCode))
        {
            return "parts_unlimited";
        }

        if (string.Equals(supplierCode, "WPS", StringComparison.OrdinalIgnoreCase))
        {
            return "wps";
        }

        if (string.Equals(supplierCode, "TURN14", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCode, "TURN 14", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplierCode, "TURN-14", StringComparison.OrdinalIgnoreCase))
        {
            return "turn14";
        }

        return Clean(supplierCode)?.ToLowerInvariant();
    }

    private sealed record SupplierProductImportRow(Guid Id, Guid GlobalProductId, string SupplierSku, string? SourceSupplierProductId);

    private sealed record FitmentLookupResult(
        IReadOnlyCollection<IndieMotoFitmentRow> Rows,
        bool PartsUnlimitedQueued,
        string? PartsUnlimitedQueueReason);

    private sealed class BatchFitmentLookupUnsupportedException(string message) : Exception(message);

    private sealed record PartsUnlimitedQueueInfo(bool Queued, string? Reason);

    private sealed record BatchFitmentRequest(
        string Intent,
        IReadOnlyCollection<string> Skus,
        int? Limit,
        string? Supplier,
        bool? QueuePartsUnlimited);

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

    private sealed record PendingFitmentLookup(
        SupplierProductImportRow SupplierProduct,
        FitmentLookupResult Lookup);

    private sealed record PendingFitmentRow(
        SupplierProductImportRow SupplierProduct,
        IndieMotoFitmentRow Row);

    private sealed record VehicleKey(
        int Year,
        string Make,
        string Model,
        string? Submodel,
        string? Engine,
        string Market)
    {
        public static VehicleKey From(IndieMotoFitmentRow row) => new(
            row.Year,
            row.Make,
            row.Model,
            row.Submodel,
            row.Engine,
            "US");

        public static VehicleKey From(GlobalVehicle vehicle) => new(
            vehicle.Year,
            vehicle.Make,
            vehicle.Model,
            vehicle.Submodel,
            vehicle.Engine,
            vehicle.Market ?? "US");
    }

    private sealed record VehicleFitmentKey(
        Guid GlobalProductId,
        Guid GlobalVehicleId,
        string? Position)
    {
        public static VehicleFitmentKey From(VehicleFitment fitment) => new(
            fitment.GlobalProductId,
            fitment.GlobalVehicleId,
            fitment.Position);
    }

    private sealed record SourceFitmentRecordKey(
        Guid SupplierId,
        string SupplierSku,
        string? SourceFitmentItemId,
        int Year,
        string Make,
        string Model,
        string? VehicleClass,
        string? Submodel,
        string? Engine)
    {
        public static SourceFitmentRecordKey From(Guid supplierId, IndieMotoFitmentRow row) => new(
            supplierId,
            row.SupplierSku,
            Clean(row.SourceFitmentItemId),
            row.Year,
            row.Make,
            row.Model,
            row.VehicleClass,
            row.Submodel,
            row.Engine);

        public static SourceFitmentRecordKey From(SupplierFitmentRecord record) => new(
            record.SupplierId,
            record.SupplierSku,
            Clean(record.SourceFitmentItemId),
            record.Year,
            record.Make,
            record.Model,
            record.VehicleClass,
            record.Submodel,
            record.Engine);
    }

    private sealed class BatchFitmentUpsertContext
    {
        private BatchFitmentUpsertContext(
            Dictionary<VehicleKey, GlobalVehicle> globalVehiclesByKey,
            Dictionary<VehicleFitmentKey, VehicleFitment> vehicleFitmentsByKey,
            Dictionary<SourceFitmentRecordKey, SupplierFitmentRecord> supplierFitmentRecordsByKey,
            Dictionary<Guid, SupplierProduct> supplierProductsById)
        {
            GlobalVehiclesByKey = globalVehiclesByKey;
            VehicleFitmentsByKey = vehicleFitmentsByKey;
            SupplierFitmentRecordsByKey = supplierFitmentRecordsByKey;
            SupplierProductsById = supplierProductsById;
        }

        public Dictionary<VehicleKey, GlobalVehicle> GlobalVehiclesByKey { get; }

        public Dictionary<VehicleFitmentKey, VehicleFitment> VehicleFitmentsByKey { get; }

        public Dictionary<SourceFitmentRecordKey, SupplierFitmentRecord> SupplierFitmentRecordsByKey { get; }

        public Dictionary<Guid, SupplierProduct> SupplierProductsById { get; }

        public static async Task<BatchFitmentUpsertContext> LoadAsync(
            IApplicationDbContext dbContext,
            Guid supplierId,
            IReadOnlyCollection<PendingFitmentRow> rows,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
            {
                return new(
                    new Dictionary<VehicleKey, GlobalVehicle>(),
                    new Dictionary<VehicleFitmentKey, VehicleFitment>(),
                    new Dictionary<SourceFitmentRecordKey, SupplierFitmentRecord>(),
                    new Dictionary<Guid, SupplierProduct>());
            }

            var vehicleKeys = rows
                .Select(x => VehicleKey.From(x.Row))
                .Distinct()
                .ToList();
            var years = vehicleKeys.Select(x => x.Year).Distinct().ToList();
            var makes = vehicleKeys.Select(x => x.Make).Distinct().ToList();
            var models = vehicleKeys.Select(x => x.Model).Distinct().ToList();
            var existingVehicles = await dbContext.GlobalVehicles
                .Where(x =>
                    years.Contains(x.Year) &&
                    makes.Contains(x.Make) &&
                    models.Contains(x.Model) &&
                    x.Market == "US")
                .ToListAsync(cancellationToken);
            var globalVehiclesByKey = existingVehicles
                .GroupBy(VehicleKey.From)
                .ToDictionary(x => x.Key, x => x.First());

            var productIds = rows.Select(x => x.SupplierProduct.GlobalProductId).Distinct().ToList();
            var vehicleIds = globalVehiclesByKey.Values.Select(x => x.Id).Distinct().ToList();
            var existingVehicleFitments = productIds.Count == 0 || vehicleIds.Count == 0
                ? []
                : await dbContext.VehicleFitments
                    .Where(x =>
                        productIds.Contains(x.GlobalProductId) &&
                        vehicleIds.Contains(x.GlobalVehicleId) &&
                        x.Position == null)
                    .ToListAsync(cancellationToken);
            var vehicleFitmentsByKey = existingVehicleFitments
                .GroupBy(VehicleFitmentKey.From)
                .ToDictionary(x => x.Key, x => x.First());

            var skus = rows.Select(x => x.Row.SupplierSku).Distinct().ToList();
            var existingSourceRecords = await dbContext.SupplierFitmentRecords
                .Where(x =>
                    x.SupplierId == supplierId &&
                    skus.Contains(x.SupplierSku) &&
                    years.Contains(x.Year) &&
                    makes.Contains(x.Make) &&
                    models.Contains(x.Model))
                .ToListAsync(cancellationToken);
            var supplierFitmentRecordsByKey = existingSourceRecords
                .GroupBy(SourceFitmentRecordKey.From)
                .ToDictionary(x => x.Key, x => x.First());

            var supplierProductIdsToUpdate = rows
                .Where(x =>
                    !string.IsNullOrWhiteSpace(Clean(x.Row.SourceSupplierProductId)) &&
                    string.IsNullOrWhiteSpace(x.SupplierProduct.SourceSupplierProductId))
                .Select(x => x.SupplierProduct.Id)
                .Distinct()
                .ToList();
            var supplierProductsById = supplierProductIdsToUpdate.Count == 0
                ? new Dictionary<Guid, SupplierProduct>()
                : await dbContext.SupplierProducts
                    .Where(x => supplierProductIdsToUpdate.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, cancellationToken);

            return new(
                globalVehiclesByKey,
                vehicleFitmentsByKey,
                supplierFitmentRecordsByKey,
                supplierProductsById);
        }
    }

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
