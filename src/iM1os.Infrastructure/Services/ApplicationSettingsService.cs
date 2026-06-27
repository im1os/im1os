using iM1os.Application.Common;
using iM1os.Application.Configuration;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class ApplicationSettingsService(IApplicationDbContext dbContext) : IApplicationSettingsService
{
    public async Task<string?> GetAsync(string key, Guid? organizationId, CancellationToken cancellationToken)
    {
        var setting = await dbContext.ApplicationSettings
            .OrderByDescending(x => x.OrganizationId == organizationId)
            .FirstOrDefaultAsync(x => x.Key == key && (x.OrganizationId == organizationId || x.OrganizationId == null), cancellationToken);

        return setting?.Value;
    }
}
