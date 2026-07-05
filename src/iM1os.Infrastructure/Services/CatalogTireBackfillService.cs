using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class CatalogTireBackfillService(IApplicationDbContext dbContext) : ICatalogTireBackfillService
{
    private const int ChunkSize = 500;

    public async Task<CatalogTireBackfillResult> BackfillAsync(CatalogTireBackfillRequest request, CancellationToken cancellationToken)
    {
        var importRun = await dbContext.SupplierConnectorImportRuns
            .SingleAsync(x => x.Id == request.ImportRunId, cancellationToken);

        var total = await CandidateProducts().CountAsync(cancellationToken);
        var targetTotal = request.MaxItems is > 0 ? Math.Min(total, request.MaxItems.Value) : total;
        importRun.ProgressProcessed = 0;
        importRun.ProgressTotal = targetTotal;
        importRun.Message = targetTotal == total
            ? $"Catalog tire parser backfill started for {targetTotal:N0} candidate products."
            : $"Catalog tire parser backfill started for first {targetTotal:N0} of {total:N0} candidate products.";
        await dbContext.SaveChangesAsync(cancellationToken);

        var processed = 0;
        var updated = 0;
        var detected = 0;
        var noTireDetected = 0;
        var failed = 0;

        while (processed < targetTotal)
        {
            var take = Math.Min(ChunkSize, targetTotal - processed);
            var productIds = await CandidateProducts()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (productIds.Count == 0)
            {
                break;
            }

            var products = await dbContext.GlobalProducts
                .Where(x => productIds.Contains(x.Id))
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var product in products)
            {
                try
                {
                    var before = Snapshot(product);
                    CatalogTireParser.Apply(
                        product,
                        product.Brand,
                        product.Manufacturer,
                        product.Category,
                        product.Description,
                        product.LongDescription,
                        product.SpecificationsJson);

                    if (HasTireData(product))
                    {
                        detected++;
                    }
                    else
                    {
                        noTireDetected++;
                    }

                    if (Snapshot(product) != before)
                    {
                        updated++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            processed += products.Count;
            importRun.ProgressProcessed = processed;
            importRun.Message = $"Catalog tire parser backfill running. Processed {processed:N0} / {targetTotal:N0}; updated {updated:N0}, detected {detected:N0}, no tire detected {noTireDetected:N0}, failed {failed:N0}.";
            await dbContext.SaveChangesAsync(cancellationToken);

            if (products.Count < take)
            {
                break;
            }
        }

        return new CatalogTireBackfillResult(request.ImportRunId, processed, updated, detected, noTireDetected, failed);
    }

    private IQueryable<GlobalProduct> CandidateProducts()
    {
        return dbContext.GlobalProducts
            .Where(x =>
                x.IsActive &&
                (x.TireWidth == null ||
                    x.TireAspectRatio == null ||
                    x.TireRimDiameter == null ||
                    x.TirePosition == null ||
                    x.TireConstruction == null ||
                    x.TireType == null ||
                    x.TireModelLine == null) &&
                ((x.Category != null && EF.Functions.ILike(x.Category, "%tire%")) ||
                    EF.Functions.ILike(x.Description, "%tire%") ||
                    (x.LongDescription != null && EF.Functions.ILike(x.LongDescription, "%tire%")) ||
                    (x.Description != null && EF.Functions.ILike(x.Description, "%/%")) ||
                    (x.LongDescription != null && EF.Functions.ILike(x.LongDescription, "%/%"))));
    }

    private static bool HasTireData(GlobalProduct product)
    {
        return product.TireWidth is not null ||
            product.TireAspectRatio is not null ||
            product.TireRimDiameter is not null ||
            product.TirePosition is not null ||
            product.TireConstruction is not null ||
            product.TireType is not null ||
            product.TireModelLine is not null;
    }

    private static TireSnapshot Snapshot(GlobalProduct product)
    {
        return new TireSnapshot(
            product.TireWidth,
            product.TireAspectRatio,
            product.TireRimDiameter,
            product.TirePosition,
            product.TireConstruction,
            product.TireType,
            product.TireModelLine);
    }

    private sealed record TireSnapshot(
        int? TireWidth,
        int? TireAspectRatio,
        int? TireRimDiameter,
        string? TirePosition,
        string? TireConstruction,
        string? TireType,
        string? TireModelLine);
}
