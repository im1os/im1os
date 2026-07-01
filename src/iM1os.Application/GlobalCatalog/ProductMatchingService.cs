using iM1os.Application.Common;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Application.GlobalCatalog;

public sealed class ProductMatchingService(IApplicationDbContext dbContext) : IProductMatchingService
{
    public async Task<ProductMatchResult> MatchAsync(ProductMatchRequest request, CancellationToken cancellationToken)
    {
        var supplierSku = Required(request.SupplierSku, "Supplier SKU");
        var supplierId = request.SupplierId == Guid.Empty
            ? throw new ArgumentException("Supplier is required.", nameof(request))
            : request.SupplierId;
        var upc = Clean(request.Upc);
        var manufacturerPartNumber = Clean(request.ManufacturerPartNumber);
        var normalizedManufacturerPartNumber = NormalizeManufacturerPartNumber(manufacturerPartNumber);
        var brand = Clean(request.Brand);
        var supplierDescription = Clean(request.SupplierDescription);

        var mappedProduct = await dbContext.SupplierProducts
            .AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.SupplierSku == supplierSku)
            .Join(
                dbContext.GlobalProducts.AsNoTracking(),
                supplierProduct => supplierProduct.GlobalProductId,
                globalProduct => globalProduct.Id,
                (supplierProduct, globalProduct) => globalProduct)
            .SingleOrDefaultAsync(cancellationToken);

        if (mappedProduct is not null)
        {
            return Matched(ProductMatchType.SupplierSkuMapping, mappedProduct, "Supplier SKU mapping", 1.00m);
        }

        if (upc is not null)
        {
            var upcMatch = await dbContext.GlobalProducts
                .AsNoTracking()
                .Where(x => x.Upc == upc && x.IsActive)
                .OrderBy(x => x.Brand)
                .ThenBy(x => x.ManufacturerPartNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (upcMatch is not null)
            {
                return Matched(ProductMatchType.Upc, upcMatch, "UPC", 0.98m);
            }
        }

        if (manufacturerPartNumber is not null)
        {
            var manufacturerPartMatches = await dbContext.GlobalProducts
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    (x.ManufacturerPartNumber == manufacturerPartNumber ||
                        (normalizedManufacturerPartNumber != null && x.NormalizedManufacturerPartNumber == normalizedManufacturerPartNumber)))
                .OrderBy(x => x.Brand)
                .ThenBy(x => x.Description)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (manufacturerPartMatches.Count == 1)
            {
                return Matched(ProductMatchType.ManufacturerPartNumber, manufacturerPartMatches[0], "Manufacturer part number", 0.92m);
            }

            if (manufacturerPartMatches.Count > 1 && brand is not null)
            {
                var brandPartMatch = manufacturerPartMatches
                    .FirstOrDefault(x => string.Equals(x.Brand, brand, StringComparison.OrdinalIgnoreCase));

                if (brandPartMatch is not null)
                {
                    return Matched(ProductMatchType.BrandAndPartNumber, brandPartMatch, "Brand and manufacturer part number", 0.96m);
                }
            }

            if (manufacturerPartMatches.Count > 1)
            {
                return await CreateManualReviewAsync(
                    supplierId,
                    supplierSku,
                    manufacturerPartNumber,
                    upc,
                    brand,
                    supplierDescription,
                    "Multiple products share the supplied manufacturer part number.",
                    manufacturerPartMatches.Select(x => Candidate(x, "Manufacturer part number candidate", 0.70m)).ToList(),
                    cancellationToken);
            }
        }

        return await CreateManualReviewAsync(
            supplierId,
            supplierSku,
            manufacturerPartNumber,
            upc,
            brand,
            supplierDescription,
            "No confident global product match was found.",
            [],
            cancellationToken);
    }

    private async Task<ProductMatchResult> CreateManualReviewAsync(
        Guid supplierId,
        string supplierSku,
        string? manufacturerPartNumber,
        string? upc,
        string? brand,
        string? supplierDescription,
        string matchReason,
        IReadOnlyCollection<ProductMatchCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var existingReviewItem = await dbContext.ProductMatchReviewItems
            .FirstOrDefaultAsync(x =>
                x.SupplierId == supplierId &&
                x.SupplierSku == supplierSku &&
                x.Status == "Open",
                cancellationToken);

        if (existingReviewItem is null)
        {
            existingReviewItem = new ProductMatchReviewItem
            {
                SupplierId = supplierId,
                SupplierSku = supplierSku,
                SupplierPartNumber = manufacturerPartNumber,
                Upc = upc,
                Brand = brand,
                SupplierDescription = supplierDescription,
                CandidateGlobalProductId = candidates.OrderByDescending(x => x.Confidence).FirstOrDefault()?.GlobalProductId,
                MatchReason = matchReason,
                Status = "Open"
            };

            dbContext.ProductMatchReviewItems.Add(existingReviewItem);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ProductMatchResult(
            ProductMatchType.ManualReview,
            null,
            0m,
            true,
            existingReviewItem.Id,
            candidates);
    }

    private static ProductMatchResult Matched(ProductMatchType matchType, GlobalProduct product, string reason, decimal confidence)
    {
        return new ProductMatchResult(
            matchType,
            product.Id,
            confidence,
            false,
            null,
            [Candidate(product, reason, confidence)]);
    }

    private static ProductMatchCandidate Candidate(GlobalProduct product, string reason, decimal confidence)
    {
        return new ProductMatchCandidate(
            product.Id,
            reason,
            confidence,
            product.Brand,
            product.ManufacturerPartNumber,
            product.Upc,
            product.Description);
    }

    private static string Required(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.")
            : value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? NormalizeManufacturerPartNumber(string? value)
    {
        var clean = Clean(value);
        if (clean is null)
        {
            return null;
        }

        var normalized = new string(clean
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        return normalized.Length == 0 ? null : normalized;
    }
}
