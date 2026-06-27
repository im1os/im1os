namespace iM1os.Workers;

public sealed class PlatformMaintenanceWorker(ILogger<PlatformMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            logger.LogInformation("iM1 OS platform maintenance heartbeat at {UtcNow}.", DateTimeOffset.UtcNow);
        }
    }
}
