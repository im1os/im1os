namespace iM1os.Application.CompanySuppliers;

public sealed record WpsDealerPricingImportRequest(Guid ImportRunId);

public sealed record PartsUnlimitedDealerPricingImportRequest(Guid ImportRunId);

public sealed record Turn14DealerPricingImportRequest(Guid ImportRunId);

public sealed record WpsDealerPricingImportResult(
    int RowsProcessed,
    int PricesUpserted,
    int UnmatchedRows,
    string? PriceFileUrl,
    DateTimeOffset? PriceFileLastModifiedUtc,
    DateTimeOffset PriceFileDownloadedAtUtc);

public sealed record SupplierDealerPricingImportResult(
    int RowsProcessed,
    int PricesUpserted,
    int UnmatchedRows,
    string? Source,
    DateTimeOffset? PriceFileLastModifiedUtc,
    DateTimeOffset PriceFileDownloadedAtUtc);

public interface IWpsDealerPricingImportService
{
    Task<WpsDealerPricingImportResult> ImportAsync(WpsDealerPricingImportRequest request, CancellationToken cancellationToken);
}

public interface IPartsUnlimitedDealerPricingImportService
{
    Task<SupplierDealerPricingImportResult> ImportAsync(PartsUnlimitedDealerPricingImportRequest request, CancellationToken cancellationToken);
}

public interface ITurn14DealerPricingImportService
{
    Task<SupplierDealerPricingImportResult> ImportAsync(Turn14DealerPricingImportRequest request, CancellationToken cancellationToken);
}
