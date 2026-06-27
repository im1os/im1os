namespace iM1os.Application.Configuration;

public interface IApplicationSettingsService
{
    Task<string?> GetAsync(string key, Guid? organizationId, CancellationToken cancellationToken);
}
