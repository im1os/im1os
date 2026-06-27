namespace iM1os.Infrastructure.Persistence;

public interface IApplicationDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
