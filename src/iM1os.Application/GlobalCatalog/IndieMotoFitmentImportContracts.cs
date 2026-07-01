namespace iM1os.Application.GlobalCatalog;

public sealed record IndieMotoFitmentImportRequest(
    string SupplierCode,
    string? Sku,
    int? MaxSkus,
    int? FitmentLimit,
    int DelayMilliseconds,
    string BaseUrl,
    bool ExcludeNla = true,
    Guid? ImportRunId = null);

public sealed record IndieMotoFitmentImportResult(
    int SkusProcessed,
    int SkusWithFitment,
    int SkusQueuedForPartsUnlimitedCrawl,
    int SkusWithoutFitment,
    int FitmentRowsProcessed,
    int SourceFitmentRowsUpserted,
    int GlobalVehiclesUpserted,
    int VehicleFitmentsUpserted,
    int FailedSkus);

public interface IIndieMotoFitmentImportService
{
    Task<IndieMotoFitmentImportResult> ImportAsync(IndieMotoFitmentImportRequest request, CancellationToken cancellationToken);
}
