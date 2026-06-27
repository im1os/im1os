using iM1os.Domain.Configuration;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class FeatureFlagServiceTests
{
    [Fact]
    public async Task IsEnabledAsync_returns_global_flag_when_tenant_override_does_not_exist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options, new NoCurrentUser(), new SystemClock(), new TenantProvider(new NoCurrentUser()));
        dbContext.FeatureFlags.Add(new FeatureFlag { Key = "dashboard.shell", IsEnabled = true });
        await dbContext.SaveChangesAsync();

        var service = new FeatureFlagService(dbContext);

        var enabled = await service.IsEnabledAsync("dashboard.shell", Guid.NewGuid(), CancellationToken.None);

        Assert.True(enabled);
    }
}
