using iM1os.Application.Common;
using iM1os.Application.Configuration;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class FeatureFlagService(IApplicationDbContext dbContext) : IFeatureFlagService
{
    public async Task<bool> IsEnabledAsync(string key, Guid? organizationId, CancellationToken cancellationToken)
    {
        var flag = await dbContext.FeatureFlags
            .OrderByDescending(x => x.OrganizationId == organizationId)
            .FirstOrDefaultAsync(x => x.Key == key && (x.OrganizationId == organizationId || x.OrganizationId == null), cancellationToken);

        return flag?.IsEnabled ?? false;
    }
}
