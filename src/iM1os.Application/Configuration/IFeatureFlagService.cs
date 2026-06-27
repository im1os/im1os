namespace iM1os.Application.Configuration;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key, Guid? organizationId, CancellationToken cancellationToken);
}
