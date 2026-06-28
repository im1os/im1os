namespace iM1os.Application.Marketing;

public interface IMarketingCmsService
{
    Task<MarketingPageDto?> GetPublishedPageAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MarketingPageSummary>> GetPagesAsync(CancellationToken cancellationToken);

    Task<MarketingPageDto?> GetPageAsync(Guid id, CancellationToken cancellationToken);

    Task<MarketingPageDto> SavePageAsync(SaveMarketingPageRequest request, CancellationToken cancellationToken);

    Task DeletePageAsync(Guid id, CancellationToken cancellationToken);

    Task<MarketingContentBlockDto> SaveBlockAsync(SaveMarketingContentBlockRequest request, CancellationToken cancellationToken);

    Task DeleteBlockAsync(Guid id, CancellationToken cancellationToken);

    Task CaptureLeadAsync(MarketingLeadRequest request, CancellationToken cancellationToken);
}
