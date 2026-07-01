namespace iM1os.Application.GlobalCatalog;

public interface IProductMatchingService
{
    Task<ProductMatchResult> MatchAsync(ProductMatchRequest request, CancellationToken cancellationToken);
}
