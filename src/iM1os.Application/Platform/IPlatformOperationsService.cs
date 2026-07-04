namespace iM1os.Application.Platform;

public interface IPlatformOperationsService
{
    Task<PlatformOperationsPage> GetOperationsAsync(CancellationToken cancellationToken);
}
