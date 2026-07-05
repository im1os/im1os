using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Application.GlobalCatalog;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Workers;

public sealed class WpsMasterItemListImportWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WpsMasterItemListImportWorker> logger) : BackgroundService
{
    private const string WpsConnectorKey = "WPS";
    private const string WpsSupplierCode = "WPS";
    private const string Turn14ConnectorKey = "TURN14";
    private const string Turn14SupplierCode = "TURN14";
    private const string PartsUnlimitedConnectorKey = "PU";
    private const string PartsUnlimitedSupplierCode = "PU";
    private const string MasterImportType = "WpsMasterFile";
    private const string FitmentImportType = "WpsFitment";
    private const string Turn14ProductLoadsheetImportType = "Turn14ProductLoadsheet";
    private const string Turn14FitmentImportType = "Turn14Fitment";
    private const string Turn14MediaEnrichmentImportType = "Turn14MediaEnrichment";
    private const string PartsUnlimitedBundleImportType = "PartsUnlimitedBundle";
    private const string PartsUnlimitedBrandImagesImportType = "PartsUnlimitedBrandImages";
    private const string PartsUnlimitedFitmentImportType = "PartsUnlimitedFitment";
    private const string CatalogConnectorKey = "CATALOG";
    private const string CatalogSupplierCode = "CATALOG";
    private const string CatalogTireBackfillImportType = "GlobalCatalogTireBackfill";
    private const string CatalogNormalizationImportType = "GlobalCatalogNormalization";
    private const string DealerPricingImportType = "WpsDealerPricing";
    private const string PartsUnlimitedDealerPricingImportType = "PartsUnlimitedDealerPricing";
    private const string Turn14DealerPricingImportType = "Turn14DealerPricing";
    private const string DefaultMasterFileUrl = "https://data-depot.s3.us-west-2.amazonaws.com/v4/downloads/master-item-list/master-item-list.json";
    private const string DefaultFitmentSourceBaseUrl = "https://saas.indie-moto.com";
    private readonly DateTimeOffset workerStartedAtUtc = DateTimeOffset.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerCount = Math.Clamp(configuration.GetValue("SupplierImports:WorkerCount", 2), 1, 8);
        logger.LogInformation("Starting supplier import worker with {WorkerCount} processor slots.", workerCount);

        var tasks = Enumerable
            .Range(1, workerCount)
            .Select(workerSlot => RunProcessorLoopAsync(workerSlot, stoppingToken))
            .Append(RunCoordinatorLoopAsync(stoppingToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task RunCoordinatorLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunCoordinatorTickAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Supplier import coordinator tick failed.");
            }

            await timer.WaitForNextTickAsync(cancellationToken);
        }
    }

    private async Task RunProcessorLoopAsync(int workerSlot, CancellationToken cancellationToken)
    {
        using var idleTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!cancellationToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                processed = await TryProcessNextRunAsync(workerSlot, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Supplier import worker slot {WorkerSlot} failed while processing the queue.", workerSlot);
            }

            if (!processed)
            {
                await idleTimer.WaitForNextTickAsync(cancellationToken);
            }
        }
    }

    private async Task RunCoordinatorTickAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        await MarkInterruptedRunsFailedAsync(dbContext, clock, cancellationToken);
        await QueueDueScheduledRunsAsync(dbContext, clock, cancellationToken);
        try
        {
            await QueueDueCompanyScheduledRunsAsync(dbContext, clock, cancellationToken);
        }
        catch (Exception exception) when (IsMissingCompanySupplierTable(exception))
        {
            logger.LogWarning("Company supplier pricing tables are not available yet; skipping company supplier pricing scheduler for this tick.");
        }
    }

    private async Task<bool> TryProcessNextRunAsync(int workerSlot, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var nextRun = await ClaimNextQueuedRunAsync(dbContext, clock.UtcNow, cancellationToken);
        CompanySupplierConnectorImportRun? nextCompanyRun = null;
        if (nextRun is null)
        {
            try
            {
                nextCompanyRun = await ClaimNextQueuedCompanyRunAsync(dbContext, clock.UtcNow, cancellationToken);
            }
            catch (Exception exception) when (IsMissingCompanySupplierTable(exception))
            {
                logger.LogWarning("Company supplier pricing tables are not available yet; skipping company supplier pricing queue for this tick.");
            }
        }
        if (nextRun is null && nextCompanyRun is null)
        {
            return false;
        }

        if (nextCompanyRun is not null)
        {
            logger.LogInformation("Worker slot {WorkerSlot} claimed company supplier import run {ImportRunId} ({ImportType}).", workerSlot, nextCompanyRun.Id, nextCompanyRun.ImportType);
            await ProcessDealerPricingRunAsync(scope.ServiceProvider, dbContext, nextCompanyRun, cancellationToken);
            return true;
        }

        if (nextRun is null)
        {
            return false;
        }

        logger.LogInformation("Worker slot {WorkerSlot} claimed supplier import run {ImportRunId} ({ImportType}).", workerSlot, nextRun.Id, nextRun.ImportType);
        if (string.Equals(nextRun.ImportType, MasterImportType, StringComparison.OrdinalIgnoreCase))
        {
            if (DisableWpsMasterFileImports())
            {
                nextRun.Status = "Blocked";
                nextRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                nextRun.Message = "WPS Master Item List import blocked because SupplierImports:DisableWpsMasterFile is enabled.";
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogWarning("Blocked WPS Master Item List import run {ImportRunId} because SupplierImports:DisableWpsMasterFile is enabled.", nextRun.Id);
                return true;
            }

            await ProcessMasterRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, Turn14ProductLoadsheetImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessTurn14ProductLoadsheetRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, PartsUnlimitedBundleImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessPartsUnlimitedBundleRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, PartsUnlimitedBrandImagesImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessPartsUnlimitedBrandImagesRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, Turn14MediaEnrichmentImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessTurn14MediaEnrichmentRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, CatalogTireBackfillImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessCatalogTireBackfillRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, CatalogNormalizationImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessCatalogNormalizationRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        if (string.Equals(nextRun.ImportType, FitmentImportType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nextRun.ImportType, Turn14FitmentImportType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nextRun.ImportType, PartsUnlimitedFitmentImportType, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessFitmentRunAsync(scope.ServiceProvider, dbContext, nextRun, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task MarkInterruptedRunsFailedAsync(IApplicationDbContext dbContext, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        var interruptedRuns = await dbContext.SupplierConnectorImportRuns
            .Where(x =>
                (x.ImportType == MasterImportType || x.ImportType == FitmentImportType || x.ImportType == Turn14ProductLoadsheetImportType || x.ImportType == Turn14FitmentImportType || x.ImportType == Turn14MediaEnrichmentImportType || x.ImportType == PartsUnlimitedBundleImportType || x.ImportType == PartsUnlimitedBrandImagesImportType || x.ImportType == PartsUnlimitedFitmentImportType || x.ImportType == CatalogTireBackfillImportType || x.ImportType == CatalogNormalizationImportType) &&
                x.Status == "Running" &&
                x.StartedAtUtc != null &&
                x.StartedAtUtc < workerStartedAtUtc)
            .ToListAsync(cancellationToken);
        List<CompanySupplierConnectorImportRun> interruptedCompanyRuns;
        try
        {
            interruptedCompanyRuns = await dbContext.CompanySupplierConnectorImportRuns
                .IgnoreQueryFilters()
                .Where(x =>
                    (x.ImportType == DealerPricingImportType || x.ImportType == PartsUnlimitedDealerPricingImportType || x.ImportType == Turn14DealerPricingImportType) &&
                    x.Status == "Running" &&
                    x.StartedAtUtc != null &&
                    x.StartedAtUtc < workerStartedAtUtc)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (IsMissingCompanySupplierTable(exception))
        {
            interruptedCompanyRuns = [];
            logger.LogWarning("Company supplier pricing tables are not available yet; skipping interrupted company supplier pricing recovery for this tick.");
        }

        if (interruptedRuns.Count == 0 && interruptedCompanyRuns.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var run in interruptedRuns)
        {
            run.Status = "Failed";
            run.CompletedAtUtc = now;
            run.Message = string.IsNullOrWhiteSpace(run.Message)
                ? "Import marked failed because the connector worker restarted before it completed."
                : $"{run.Message} Import marked failed because the connector worker restarted before it completed.";
            logger.LogWarning("Marked interrupted WPS import run {ImportRunId} as failed after worker restart.", run.Id);
        }

        foreach (var run in interruptedCompanyRuns)
        {
            run.Status = "Failed";
            run.CompletedAtUtc = now;
            run.Message = string.IsNullOrWhiteSpace(run.Message)
                ? "Import marked failed because the connector worker restarted before it completed."
                : $"{run.Message} Import marked failed because the connector worker restarted before it completed.";
            logger.LogWarning("Marked interrupted company supplier pricing import run {ImportRunId} as failed after worker restart.", run.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task QueueDueScheduledRunsAsync(IApplicationDbContext dbContext, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        var wpsConfiguration = await (
            from connector in dbContext.SupplierConnectorConfigurations
            join supplier in dbContext.Suppliers on connector.SupplierId equals supplier.Id
            where supplier.Code == WpsSupplierCode && connector.ConnectorKey == WpsConnectorKey
            select connector)
            .SingleOrDefaultAsync(cancellationToken);

        var now = clock.UtcNow;
        var changed = false;

        if (wpsConfiguration is not null && wpsConfiguration.IsEnabled)
        {
            if (!DisableWpsMasterFileImports() &&
                wpsConfiguration.ImportMasterFileOnSchedule &&
                IsDue(now, wpsConfiguration.LastMasterFileScheduledAtUtc, wpsConfiguration.MasterFileScheduleCadenceMinutes) &&
                !await HasOpenRunAsync(dbContext, wpsConfiguration.Id, MasterImportType, cancellationToken))
            {
                var source = string.IsNullOrWhiteSpace(wpsConfiguration.MasterFileUrl) ? DefaultMasterFileUrl : wpsConfiguration.MasterFileUrl;
                dbContext.SupplierConnectorImportRuns.Add(new()
                {
                    SupplierConnectorConfigurationId = wpsConfiguration.Id,
                    ImportType = MasterImportType,
                    Status = string.IsNullOrWhiteSpace(source) ? "Blocked" : "Queued",
                    RequestedAtUtc = now,
                    Source = source,
                    ProgressProcessed = 0,
                    ProgressTotal = wpsConfiguration.MasterFileScheduleMaxItems,
                    ParametersJson = JsonSerializer.Serialize(new
                    {
                        ImportProducts = true,
                        ImportSupplierPricing = true,
                        ImportFitment = false,
                        UpdateExistingProducts = true,
                        CreateMissingProducts = true,
                        ImportMode = "Scheduled",
                        MaxItems = wpsConfiguration.MasterFileScheduleMaxItems
                    }),
                    Message = wpsConfiguration.MasterFileScheduleMaxItems is null
                        ? "Scheduled WPS Master Item List import queued."
                        : $"Scheduled WPS Master Item List import queued for first {wpsConfiguration.MasterFileScheduleMaxItems.Value} items."
                });
                wpsConfiguration.LastMasterFileScheduledAtUtc = now;
                changed = true;
            }
            else if (DisableWpsMasterFileImports() && wpsConfiguration.ImportMasterFileOnSchedule)
            {
                logger.LogWarning("Skipped scheduled WPS Master Item List import because SupplierImports:DisableWpsMasterFile is enabled.");
            }

            if (wpsConfiguration.ImportFitmentOnSchedule &&
                IsDue(now, wpsConfiguration.LastFitmentScheduledAtUtc, wpsConfiguration.FitmentScheduleCadenceMinutes) &&
                !await HasOpenRunAsync(dbContext, wpsConfiguration.Id, FitmentImportType, cancellationToken))
            {
                var source = string.IsNullOrWhiteSpace(wpsConfiguration.FitmentSourceBaseUrl) ? DefaultFitmentSourceBaseUrl : wpsConfiguration.FitmentSourceBaseUrl;
                dbContext.SupplierConnectorImportRuns.Add(new()
                {
                    SupplierConnectorConfigurationId = wpsConfiguration.Id,
                    ImportType = FitmentImportType,
                    Status = "Queued",
                    RequestedAtUtc = now,
                    Source = source,
                    ProgressProcessed = 0,
                    ProgressTotal = wpsConfiguration.FitmentScheduleMaxSkus,
                    ParametersJson = JsonSerializer.Serialize(new
                    {
                        SupplierCode = WpsSupplierCode,
                        ImportMode = "Scheduled",
                        MaxSkus = wpsConfiguration.FitmentScheduleMaxSkus,
                        FitmentLimit = wpsConfiguration.FitmentScheduleFitmentLimit,
                        DelayMilliseconds = wpsConfiguration.FitmentScheduleDelayMilliseconds,
                        BaseUrl = source,
                        ExcludeNla = true
                    }),
                    Message = wpsConfiguration.FitmentScheduleMaxSkus is null
                        ? "Scheduled WPS fitment import queued for active SKUs."
                        : $"Scheduled WPS fitment import queued for first {wpsConfiguration.FitmentScheduleMaxSkus.Value} active SKUs."
                });
                wpsConfiguration.LastFitmentScheduledAtUtc = now;
                changed = true;
            }
        }

        var turn14Configuration = await (
            from connector in dbContext.SupplierConnectorConfigurations
            join supplier in dbContext.Suppliers on connector.SupplierId equals supplier.Id
            where supplier.Code == Turn14SupplierCode && connector.ConnectorKey == Turn14ConnectorKey
            select connector)
            .SingleOrDefaultAsync(cancellationToken);

        if (turn14Configuration is not null &&
            turn14Configuration.IsEnabled &&
            turn14Configuration.ImportMasterFileOnSchedule &&
            IsDue(now, turn14Configuration.LastMasterFileScheduledAtUtc, turn14Configuration.MasterFileScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, turn14Configuration.Id, Turn14ProductLoadsheetImportType, cancellationToken))
        {
            var source = string.IsNullOrWhiteSpace(turn14Configuration.BaseApiUrl) ? "https://turn14.com" : turn14Configuration.BaseApiUrl;
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = turn14Configuration.Id,
                ImportType = Turn14ProductLoadsheetImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = source,
                ProgressProcessed = 0,
                ProgressTotal = turn14Configuration.MasterFileScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    ImportProducts = true,
                    ImportSupplierPricing = true,
                    UpdateExistingProducts = true,
                    CreateMissingProducts = true,
                    ImportMode = "Scheduled",
                    MaxItems = turn14Configuration.MasterFileScheduleMaxItems
                }),
                Message = turn14Configuration.MasterFileScheduleMaxItems is null
                    ? "Scheduled Turn14 product loadsheet import queued."
                    : $"Scheduled Turn14 product loadsheet import queued for first {turn14Configuration.MasterFileScheduleMaxItems.Value} rows."
            });
            turn14Configuration.LastMasterFileScheduledAtUtc = now;
            changed = true;
        }

        if (turn14Configuration is not null &&
            turn14Configuration.IsEnabled &&
            turn14Configuration.ImportFitmentOnSchedule &&
            IsDue(now, turn14Configuration.LastFitmentScheduledAtUtc, turn14Configuration.FitmentScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, turn14Configuration.Id, Turn14FitmentImportType, cancellationToken))
        {
            var source = string.IsNullOrWhiteSpace(turn14Configuration.FitmentSourceBaseUrl) ? DefaultFitmentSourceBaseUrl : turn14Configuration.FitmentSourceBaseUrl;
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = turn14Configuration.Id,
                ImportType = Turn14FitmentImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = source,
                ProgressProcessed = 0,
                ProgressTotal = turn14Configuration.FitmentScheduleMaxSkus,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    SupplierCode = Turn14SupplierCode,
                    ImportMode = "Scheduled",
                    MaxSkus = turn14Configuration.FitmentScheduleMaxSkus,
                    FitmentLimit = turn14Configuration.FitmentScheduleFitmentLimit,
                    DelayMilliseconds = turn14Configuration.FitmentScheduleDelayMilliseconds,
                    BaseUrl = source,
                    ExcludeNla = true
                }),
                Message = turn14Configuration.FitmentScheduleMaxSkus is null
                    ? "Scheduled Turn14 fitment import queued for active SKUs."
                    : $"Scheduled Turn14 fitment import queued for first {turn14Configuration.FitmentScheduleMaxSkus.Value} active SKUs."
            });
            turn14Configuration.LastFitmentScheduledAtUtc = now;
            changed = true;
        }

        if (turn14Configuration is not null &&
            turn14Configuration.IsEnabled &&
            turn14Configuration.ImportMediaOnSchedule &&
            IsDue(now, turn14Configuration.LastMediaScheduledAtUtc, turn14Configuration.MediaScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, turn14Configuration.Id, Turn14MediaEnrichmentImportType, cancellationToken))
        {
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = turn14Configuration.Id,
                ImportType = Turn14MediaEnrichmentImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = "https://api.turn14.com/v1/items/data/{itemId}",
                ProgressProcessed = 0,
                ProgressTotal = turn14Configuration.MediaScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    ImportMode = "Scheduled",
                    MaxItems = turn14Configuration.MediaScheduleMaxItems,
                    DelayMilliseconds = turn14Configuration.MediaScheduleDelayMilliseconds
                }),
                Message = turn14Configuration.MediaScheduleMaxItems is null
                    ? "Scheduled Turn14 media enrichment queued for products missing media."
                    : $"Scheduled Turn14 media enrichment queued for first {turn14Configuration.MediaScheduleMaxItems.Value} products missing media."
            });
            turn14Configuration.LastMediaScheduledAtUtc = now;
            changed = true;
        }

        var partsUnlimitedConfiguration = await (
            from connector in dbContext.SupplierConnectorConfigurations
            join supplier in dbContext.Suppliers on connector.SupplierId equals supplier.Id
            where supplier.Code == PartsUnlimitedSupplierCode && connector.ConnectorKey == PartsUnlimitedConnectorKey
            select connector)
            .SingleOrDefaultAsync(cancellationToken);

        if (partsUnlimitedConfiguration is not null &&
            partsUnlimitedConfiguration.IsEnabled &&
            partsUnlimitedConfiguration.ImportMasterFileOnSchedule &&
            IsDue(now, partsUnlimitedConfiguration.LastMasterFileScheduledAtUtc, partsUnlimitedConfiguration.MasterFileScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, partsUnlimitedConfiguration.Id, PartsUnlimitedBundleImportType, cancellationToken))
        {
            var source = string.IsNullOrWhiteSpace(partsUnlimitedConfiguration.BaseApiUrl)
                ? "https://api.parts-unlimited.com/api"
                : partsUnlimitedConfiguration.BaseApiUrl;
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = partsUnlimitedConfiguration.Id,
                ImportType = PartsUnlimitedBundleImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = source,
                ProgressProcessed = 0,
                ProgressTotal = partsUnlimitedConfiguration.MasterFileScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    ImportProducts = true,
                    ImportSupplierPricing = true,
                    UpdateExistingProducts = true,
                    CreateMissingProducts = true,
                    ImportMode = "Scheduled",
                    MaxItems = partsUnlimitedConfiguration.MasterFileScheduleMaxItems
                }),
                Message = partsUnlimitedConfiguration.MasterFileScheduleMaxItems is null
                    ? "Scheduled Parts Unlimited bundle import queued."
                    : $"Scheduled Parts Unlimited bundle import queued for first {partsUnlimitedConfiguration.MasterFileScheduleMaxItems.Value} parts."
            });
            partsUnlimitedConfiguration.LastMasterFileScheduledAtUtc = now;
            changed = true;
        }

        if (partsUnlimitedConfiguration is not null &&
            partsUnlimitedConfiguration.IsEnabled &&
            partsUnlimitedConfiguration.ImportMediaOnSchedule &&
            IsDue(now, partsUnlimitedConfiguration.LastMediaScheduledAtUtc, partsUnlimitedConfiguration.MediaScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, partsUnlimitedConfiguration.Id, PartsUnlimitedBrandImagesImportType, cancellationToken))
        {
            var partsUnlimitedOptions = PartsUnlimitedConnectorOptions.FromConfiguration(partsUnlimitedConfiguration);
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = partsUnlimitedConfiguration.Id,
                ImportType = PartsUnlimitedBrandImagesImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = "Parts Unlimited brand reports",
                ProgressProcessed = 0,
                ProgressTotal = partsUnlimitedConfiguration.MediaScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    ImportBrandFiles = true,
                    BrandFileUrls = partsUnlimitedOptions.BrandFileUrls,
                    BrandFileMaxFiles = partsUnlimitedConfiguration.MediaScheduleMaxItems ?? partsUnlimitedOptions.BrandFileMaxFiles,
                    ImportMode = "Scheduled"
                }),
                Message = partsUnlimitedConfiguration.MediaScheduleMaxItems is null
                    ? "Scheduled Parts Unlimited brand image import queued."
                    : $"Scheduled Parts Unlimited brand image import queued for first {partsUnlimitedConfiguration.MediaScheduleMaxItems.Value} brand files."
            });
            partsUnlimitedConfiguration.LastMediaScheduledAtUtc = now;
            changed = true;
        }

        if (partsUnlimitedConfiguration is not null &&
            partsUnlimitedConfiguration.IsEnabled &&
            partsUnlimitedConfiguration.ImportFitmentOnSchedule &&
            IsDue(now, partsUnlimitedConfiguration.LastFitmentScheduledAtUtc, partsUnlimitedConfiguration.FitmentScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, partsUnlimitedConfiguration.Id, PartsUnlimitedFitmentImportType, cancellationToken))
        {
            var source = string.IsNullOrWhiteSpace(partsUnlimitedConfiguration.FitmentSourceBaseUrl) ? DefaultFitmentSourceBaseUrl : partsUnlimitedConfiguration.FitmentSourceBaseUrl;
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = partsUnlimitedConfiguration.Id,
                ImportType = PartsUnlimitedFitmentImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = source,
                ProgressProcessed = 0,
                ProgressTotal = partsUnlimitedConfiguration.FitmentScheduleMaxSkus,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    SupplierCode = PartsUnlimitedSupplierCode,
                    ImportMode = "Scheduled",
                    MaxSkus = partsUnlimitedConfiguration.FitmentScheduleMaxSkus,
                    FitmentLimit = partsUnlimitedConfiguration.FitmentScheduleFitmentLimit,
                    DelayMilliseconds = partsUnlimitedConfiguration.FitmentScheduleDelayMilliseconds,
                    BaseUrl = source,
                    ExcludeNla = true
                }),
                Message = partsUnlimitedConfiguration.FitmentScheduleMaxSkus is null
                    ? "Scheduled Parts Unlimited fitment import queued for active SKUs."
                    : $"Scheduled Parts Unlimited fitment import queued for first {partsUnlimitedConfiguration.FitmentScheduleMaxSkus.Value} active SKUs."
            });
            partsUnlimitedConfiguration.LastFitmentScheduledAtUtc = now;
            changed = true;
        }

        var catalogConfiguration = await (
            from connector in dbContext.SupplierConnectorConfigurations
            join supplier in dbContext.Suppliers on connector.SupplierId equals supplier.Id
            where supplier.Code == CatalogSupplierCode && connector.ConnectorKey == CatalogConnectorKey
            select connector)
            .SingleOrDefaultAsync(cancellationToken);

        if (catalogConfiguration is not null &&
            catalogConfiguration.IsEnabled &&
            catalogConfiguration.ImportMasterFileOnSchedule &&
            IsDue(now, catalogConfiguration.LastMasterFileScheduledAtUtc, catalogConfiguration.MasterFileScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, catalogConfiguration.Id, CatalogTireBackfillImportType, cancellationToken))
        {
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = catalogConfiguration.Id,
                ImportType = CatalogTireBackfillImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = "platform.global_products",
                ProgressProcessed = 0,
                ProgressTotal = catalogConfiguration.MasterFileScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    ImportMode = "Scheduled",
                    MaxItems = catalogConfiguration.MasterFileScheduleMaxItems
                }),
                Message = catalogConfiguration.MasterFileScheduleMaxItems is null
                    ? "Scheduled catalog tire parser backfill queued."
                    : $"Scheduled catalog tire parser backfill queued for first {catalogConfiguration.MasterFileScheduleMaxItems.Value:N0} candidate products."
            });
            catalogConfiguration.LastMasterFileScheduledAtUtc = now;
            changed = true;
        }

        if (catalogConfiguration is not null &&
            catalogConfiguration.IsEnabled &&
            catalogConfiguration.ImportMediaOnSchedule &&
            IsDue(now, catalogConfiguration.LastMediaScheduledAtUtc, catalogConfiguration.MediaScheduleCadenceMinutes) &&
            !await HasOpenRunAsync(dbContext, catalogConfiguration.Id, CatalogNormalizationImportType, cancellationToken))
        {
            dbContext.SupplierConnectorImportRuns.Add(new()
            {
                SupplierConnectorConfigurationId = catalogConfiguration.Id,
                ImportType = CatalogNormalizationImportType,
                Status = "Queued",
                RequestedAtUtc = now,
                Source = "platform.supplier_products",
                ProgressProcessed = 0,
                ProgressTotal = catalogConfiguration.MediaScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    ImportMode = "Scheduled",
                    MaxItems = catalogConfiguration.MediaScheduleMaxItems
                }),
                Message = catalogConfiguration.MediaScheduleMaxItems is null
                    ? "Scheduled catalog normalization queued."
                    : $"Scheduled catalog normalization queued for first {catalogConfiguration.MediaScheduleMaxItems.Value:N0} supplier products."
            });
            catalogConfiguration.LastMediaScheduledAtUtc = now;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task QueueDueCompanyScheduledRunsAsync(IApplicationDbContext dbContext, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        var configurations = await (
            from connector in dbContext.CompanySupplierConnectorConfigurations.IgnoreQueryFilters()
            join supplier in dbContext.Suppliers on connector.SupplierId equals supplier.Id
            where (supplier.Code == WpsSupplierCode || supplier.Code == PartsUnlimitedSupplierCode || supplier.Code == Turn14SupplierCode) &&
                connector.IsEnabled &&
                connector.SyncDealerPricingOnSchedule
            select connector)
            .ToListAsync(cancellationToken);

        if (configurations.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        var changed = false;
        foreach (var configuration in configurations)
        {
            if (!IsDue(now, configuration.LastDealerPricingScheduledAtUtc, configuration.DealerPricingScheduleIntervalMinutes) ||
                await HasOpenCompanyRunAsync(dbContext, configuration.OrganizationId, configuration.Id, CompanyDealerPricingImportType(configuration.ConnectorKey), cancellationToken))
            {
                continue;
            }

            dbContext.CompanySupplierConnectorImportRuns.Add(new()
            {
                OrganizationId = configuration.OrganizationId,
                CompanySupplierConnectorConfigurationId = configuration.Id,
                ImportType = CompanyDealerPricingImportType(configuration.ConnectorKey),
                Status = "Queued",
                RequestedAtUtc = now,
                Source = configuration.ConnectorKey,
                ProgressProcessed = 0,
                ProgressTotal = configuration.DealerPricingScheduleMaxItems,
                ParametersJson = JsonSerializer.Serialize(new
                {
                    MaxItems = configuration.DealerPricingScheduleMaxItems
                }),
                Message = configuration.DealerPricingScheduleMaxItems is null
                    ? $"Scheduled {CompanyDealerPricingDisplayName(configuration.ConnectorKey)} dealer pricing sync queued."
                    : $"Scheduled {CompanyDealerPricingDisplayName(configuration.ConnectorKey)} dealer pricing sync queued for first {configuration.DealerPricingScheduleMaxItems.Value} items."
            });
            configuration.LastDealerPricingScheduledAtUtc = now;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<SupplierConnectorImportRun?> ClaimNextQueuedRunAsync(IApplicationDbContext dbContext, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
    {
        while (true)
        {
            var candidate = await dbContext.SupplierConnectorImportRuns
                .Where(x =>
                    (x.ImportType == MasterImportType || x.ImportType == FitmentImportType || x.ImportType == Turn14ProductLoadsheetImportType || x.ImportType == Turn14FitmentImportType || x.ImportType == Turn14MediaEnrichmentImportType || x.ImportType == PartsUnlimitedBundleImportType || x.ImportType == PartsUnlimitedBrandImagesImportType || x.ImportType == PartsUnlimitedFitmentImportType || x.ImportType == CatalogTireBackfillImportType || x.ImportType == CatalogNormalizationImportType) &&
                    x.Status == "Queued" &&
                    !dbContext.SupplierConnectorImportRuns.Any(r =>
                        r.SupplierConnectorConfigurationId == x.SupplierConnectorConfigurationId &&
                        r.ImportType == x.ImportType &&
                        r.Status == "Running"))
                .OrderBy(x => x.RequestedAtUtc)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (candidate is null)
            {
                return null;
            }

            var claimed = await dbContext.SupplierConnectorImportRuns
                .Where(x =>
                    x.Id == candidate.Id &&
                    x.Status == "Queued" &&
                    !dbContext.SupplierConnectorImportRuns.Any(r =>
                        r.SupplierConnectorConfigurationId == x.SupplierConnectorConfigurationId &&
                        r.ImportType == x.ImportType &&
                        r.Status == "Running"))
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(x => x.Status, "Running")
                        .SetProperty(x => x.StartedAtUtc, claimedAtUtc),
                    cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            if (dbContext is DbContext efDbContext)
            {
                efDbContext.ChangeTracker.Clear();
            }

            return await dbContext.SupplierConnectorImportRuns
                .SingleAsync(x => x.Id == candidate.Id, cancellationToken);
        }
    }

    private static async Task<CompanySupplierConnectorImportRun?> ClaimNextQueuedCompanyRunAsync(IApplicationDbContext dbContext, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
    {
        while (true)
        {
            var candidate = await dbContext.CompanySupplierConnectorImportRuns
                .IgnoreQueryFilters()
                .Where(x =>
                    (x.ImportType == DealerPricingImportType || x.ImportType == PartsUnlimitedDealerPricingImportType || x.ImportType == Turn14DealerPricingImportType) &&
                    x.Status == "Queued" &&
                    !dbContext.CompanySupplierConnectorImportRuns.IgnoreQueryFilters().Any(r =>
                        r.OrganizationId == x.OrganizationId &&
                        r.CompanySupplierConnectorConfigurationId == x.CompanySupplierConnectorConfigurationId &&
                        r.ImportType == x.ImportType &&
                        r.Status == "Running"))
                .OrderBy(x => x.RequestedAtUtc)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (candidate is null)
            {
                return null;
            }

            var claimed = await dbContext.CompanySupplierConnectorImportRuns
                .IgnoreQueryFilters()
                .Where(x =>
                    x.Id == candidate.Id &&
                    x.Status == "Queued" &&
                    !dbContext.CompanySupplierConnectorImportRuns.IgnoreQueryFilters().Any(r =>
                        r.OrganizationId == x.OrganizationId &&
                        r.CompanySupplierConnectorConfigurationId == x.CompanySupplierConnectorConfigurationId &&
                        r.ImportType == x.ImportType &&
                        r.Status == "Running"))
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(x => x.Status, "Running")
                        .SetProperty(x => x.StartedAtUtc, claimedAtUtc),
                    cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            if (dbContext is DbContext efDbContext)
            {
                efDbContext.ChangeTracker.Clear();
            }

            return await dbContext.CompanySupplierConnectorImportRuns
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == candidate.Id, cancellationToken);
        }
    }

    private async Task ProcessMasterRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            var importService = serviceProvider.GetRequiredService<IWpsMasterItemListImportService>();
            var maxItems = ReadMaxItems(nextRun.ParametersJson);
            logger.LogInformation("Starting WPS Master Item List import run {ImportRunId} with MaxItems={MaxItems}.", nextRun.Id, maxItems);
            var result = await importService.ImportAsync(new WpsMasterItemListImportRequest(nextRun.Id, maxItems), cancellationToken);
            logger.LogInformation(
                "Completed WPS import run {ImportRunId}. Processed={Processed}, CreatedGlobalProducts={CreatedGlobalProducts}, CreatedSupplierProducts={CreatedSupplierProducts}.",
                result.ImportRunId,
                result.Processed,
                result.CreatedGlobalProducts,
                result.CreatedSupplierProducts);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "WPS Master Item List import run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessTurn14ProductLoadsheetRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            var importService = serviceProvider.GetRequiredService<ITurn14ProductLoadsheetImportService>();
            var maxItems = ReadMaxItems(nextRun.ParametersJson);
            logger.LogInformation("Starting Turn14 product loadsheet import run {ImportRunId} with MaxItems={MaxItems}.", nextRun.Id, maxItems);
            var result = await importService.ImportAsync(new Turn14ProductLoadsheetImportRequest(nextRun.Id, maxItems), cancellationToken);
            logger.LogInformation(
                "Completed Turn14 import run {ImportRunId}. Processed={Processed}, CreatedGlobalProducts={CreatedGlobalProducts}, CreatedSupplierProducts={CreatedSupplierProducts}.",
                result.ImportRunId,
                result.Processed,
                result.CreatedGlobalProducts,
                result.CreatedSupplierProducts);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Turn14 product loadsheet import run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessPartsUnlimitedBundleRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            var importService = serviceProvider.GetRequiredService<IPartsUnlimitedBundleImportService>();
            var maxItems = ReadMaxItems(nextRun.ParametersJson);
            logger.LogInformation("Starting Parts Unlimited bundle import run {ImportRunId} with MaxItems={MaxItems}.", nextRun.Id, maxItems);
            var result = await importService.ImportAsync(new PartsUnlimitedBundleImportRequest(nextRun.Id, maxItems), cancellationToken);
            logger.LogInformation(
                "Completed Parts Unlimited import run {ImportRunId}. Processed={Processed}, CreatedGlobalProducts={CreatedGlobalProducts}, CreatedSupplierProducts={CreatedSupplierProducts}.",
                result.ImportRunId,
                result.Processed,
                result.CreatedGlobalProducts,
                result.CreatedSupplierProducts);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Parts Unlimited bundle import run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessPartsUnlimitedBrandImagesRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            var importService = serviceProvider.GetRequiredService<IPartsUnlimitedBrandImageImportService>();
            var maxFiles = ReadBrandFileMaxFiles(nextRun.ParametersJson);
            logger.LogInformation("Starting Parts Unlimited brand image import run {ImportRunId} with MaxFiles={MaxFiles}.", nextRun.Id, maxFiles);
            var result = await importService.ImportAsync(new PartsUnlimitedBrandImageImportRequest(nextRun.Id, maxFiles), cancellationToken);
            logger.LogInformation(
                "Completed Parts Unlimited brand image import run {ImportRunId}. BrandFiles={BrandFiles}, ImageRows={ImageRows}, Updated={Updated}, Unmatched={Unmatched}.",
                nextRun.Id,
                result.BrandFilesProcessed,
                result.BrandImageRowsProcessed,
                result.BrandImagesUpdated,
                result.BrandImageRowsUnmatched);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Parts Unlimited brand image import run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, $"Parts Unlimited brand image import failed: {FailureMessage(exception)}", cancellationToken);
        }
    }

    private async Task ProcessTurn14MediaEnrichmentRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            var importService = serviceProvider.GetRequiredService<ITurn14MediaEnrichmentService>();
            var parameters = MediaEnrichmentRunParameters.FromJson(nextRun.ParametersJson);
            logger.LogInformation("Starting Turn14 media enrichment run {ImportRunId} with MaxItems={MaxItems}.", nextRun.Id, parameters.MaxItems);
            var result = await importService.ImportAsync(
                new Turn14MediaEnrichmentRunRequest(
                    nextRun.Id,
                    parameters.MaxItems,
                    parameters.DelayMilliseconds ?? 750),
                cancellationToken);
            logger.LogInformation(
                "Completed Turn14 media enrichment run {ImportRunId}. Processed={Processed}, Updated={UpdatedProducts}, Skipped={SkippedProducts}, StoppedForRateLimit={StoppedForRateLimit}.",
                result.ImportRunId,
                result.Processed,
                result.UpdatedProducts,
                result.SkippedProducts,
                result.StoppedForRateLimit);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Turn14 media enrichment run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessCatalogTireBackfillRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            nextRun.Status = "Running";
            nextRun.StartedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = 0;
            nextRun.ProgressTotal = ReadMaxItems(nextRun.ParametersJson);
            nextRun.Message = "Catalog tire parser backfill started.";
            await dbContext.SaveChangesAsync(cancellationToken);

            var importService = serviceProvider.GetRequiredService<ICatalogTireBackfillService>();
            var maxItems = ReadMaxItems(nextRun.ParametersJson);
            logger.LogInformation("Starting catalog tire parser backfill run {ImportRunId} with MaxItems={MaxItems}.", nextRun.Id, maxItems);
            var result = await importService.BackfillAsync(new CatalogTireBackfillRequest(nextRun.Id, maxItems), cancellationToken);

            nextRun.Status = result.Failed == 0 ? "Completed" : "CompletedWithErrors";
            nextRun.CompletedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = result.Processed;
            nextRun.ProgressTotal ??= result.Processed;
            nextRun.Message = $"Catalog tire parser backfill completed. Processed {result.Processed:N0}, updated {result.Updated:N0}, detected tire data on {result.TireProductsDetected:N0}, no tire detected {result.NoTireDetected:N0}, failed {result.Failed:N0}.";
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Completed catalog tire parser backfill run {ImportRunId}. Processed={Processed}, Updated={Updated}, TireProductsDetected={TireProductsDetected}, Failed={Failed}.",
                nextRun.Id,
                result.Processed,
                result.Updated,
                result.TireProductsDetected,
                result.Failed);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Catalog tire parser backfill run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessCatalogNormalizationRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            nextRun.Status = "Running";
            nextRun.StartedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = 0;
            nextRun.ProgressTotal = ReadMaxItems(nextRun.ParametersJson);
            nextRun.Message = "Catalog normalization started.";
            await dbContext.SaveChangesAsync(cancellationToken);

            var importService = serviceProvider.GetRequiredService<ICatalogNormalizationService>();
            var maxItems = ReadMaxItems(nextRun.ParametersJson);
            logger.LogInformation("Starting catalog normalization run {ImportRunId} with MaxItems={MaxItems}.", nextRun.Id, maxItems);
            var result = await importService.NormalizeAsync(new CatalogNormalizationRequest(nextRun.Id, maxItems), cancellationToken);

            nextRun.Status = result.Failed == 0 ? "Completed" : "CompletedWithErrors";
            nextRun.CompletedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = result.ProcessedSupplierProducts;
            nextRun.ProgressTotal ??= result.ProcessedSupplierProducts;
            nextRun.Message = $"Catalog normalization completed. Processed {result.ProcessedSupplierProducts:N0} supplier products, created {result.CreatedCanonicalItems:N0} canonical items, updated {result.UpdatedCanonicalItems:N0}, upserted {result.UpsertedSupplierOffers:N0} supplier offers, added {result.AddedIdentifiers:N0} identifiers, {result.AddedSources:N0} sources, {result.AddedFitments:N0} fitments, failed {result.Failed:N0}.";
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Completed catalog normalization run {ImportRunId}. Processed={Processed}, CreatedCanonicalItems={CreatedCanonicalItems}, Offers={Offers}, Identifiers={Identifiers}, Sources={Sources}, Fitments={Fitments}, Failed={Failed}.",
                nextRun.Id,
                result.ProcessedSupplierProducts,
                result.CreatedCanonicalItems,
                result.UpsertedSupplierOffers,
                result.AddedIdentifiers,
                result.AddedSources,
                result.AddedFitments,
                result.Failed);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Catalog normalization run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessFitmentRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, SupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            var importService = serviceProvider.GetRequiredService<IIndieMotoFitmentImportService>();
            var parameters = FitmentRunParameters.FromJson(nextRun.ParametersJson);
            nextRun.Status = "Running";
            nextRun.StartedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = 0;
            nextRun.ProgressTotal = parameters.Sku is null ? parameters.MaxSkus : 1;
            var supplierCode = parameters.SupplierCode ?? WpsSupplierCode;
            nextRun.Message = parameters.Sku is null
                ? $"{supplierCode} fitment import started."
                : $"{supplierCode} fitment import started for SKU {parameters.Sku}.";
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Starting {SupplierCode} fitment import run {ImportRunId} with MaxSkus={MaxSkus}.", supplierCode, nextRun.Id, parameters.MaxSkus);
            var result = await importService.ImportAsync(
                new IndieMotoFitmentImportRequest(
                    supplierCode,
                    parameters.Sku,
                    parameters.MaxSkus,
                    parameters.FitmentLimit,
                    parameters.DelayMilliseconds ?? 250,
                    parameters.BaseUrl ?? DefaultFitmentSourceBaseUrl,
                    parameters.ExcludeNla,
                    nextRun.Id),
                cancellationToken);

            nextRun.Status = "Completed";
            nextRun.CompletedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = result.SkusProcessed + result.FailedSkus;
            nextRun.ProgressTotal ??= result.SkusProcessed + result.FailedSkus;
            nextRun.Message = $"{supplierCode} fitment import completed. Processed {result.SkusProcessed} SKUs, {result.SkusWithFitment} with fitment, {result.SkusQueuedForPartsUnlimitedCrawl} queued for Parts Unlimited crawl, {result.SkusWithoutFitment} with no fitment, upserted {result.SourceFitmentRowsUpserted} source rows, failed {result.FailedSkus}.";
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Completed {SupplierCode} fitment import run {ImportRunId}. SkusProcessed={SkusProcessed}, SkusWithFitment={SkusWithFitment}, SkusQueuedForPartsUnlimitedCrawl={SkusQueuedForPartsUnlimitedCrawl}, SkusWithoutFitment={SkusWithoutFitment}, FitmentRows={FitmentRows}, FailedSkus={FailedSkus}.",
                supplierCode,
                nextRun.Id,
                result.SkusProcessed,
                result.SkusWithFitment,
                result.SkusQueuedForPartsUnlimitedCrawl,
                result.SkusWithoutFitment,
                result.FitmentRowsProcessed,
                result.FailedSkus);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fitment import run {ImportRunId} failed.", nextRun.Id);
            await MarkSupplierRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private async Task ProcessDealerPricingRunAsync(IServiceProvider serviceProvider, IApplicationDbContext dbContext, CompanySupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        try
        {
            nextRun.Status = "Running";
            nextRun.StartedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = 0;
            nextRun.ProgressTotal = ReadMaxItems(nextRun.ParametersJson);
            var supplierName = CompanyDealerPricingDisplayName(nextRun.ImportType);
            nextRun.Message = $"{supplierName} dealer pricing sync started.";
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Starting company {SupplierName} dealer pricing sync run {ImportRunId}.", supplierName, nextRun.Id);
            var result = await RunCompanyDealerPricingImportAsync(serviceProvider, nextRun, cancellationToken);

            nextRun.Status = "Completed";
            nextRun.CompletedAtUtc = DateTimeOffset.UtcNow;
            nextRun.ProgressProcessed = result.RowsProcessed;
            nextRun.ProgressTotal ??= result.RowsProcessed;
            nextRun.Message = $"{supplierName} dealer pricing sync completed. Processed {result.RowsProcessed} rows, upserted {result.PricesUpserted} company prices, unmatched {result.UnmatchedRows}. Price source {(result.PriceFileLastModifiedUtc?.ToString("yyyy-MM-dd HH:mm") ?? result.PriceFileDownloadedAtUtc.ToString("yyyy-MM-dd HH:mm"))}.";
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Completed company {SupplierName} dealer pricing sync run {ImportRunId}. RowsProcessed={RowsProcessed}, PricesUpserted={PricesUpserted}, UnmatchedRows={UnmatchedRows}.",
                supplierName,
                nextRun.Id,
                result.RowsProcessed,
                result.PricesUpserted,
                result.UnmatchedRows);
        }
        catch (Exception exception)
        {
            var supplierName = CompanyDealerPricingDisplayName(nextRun.ImportType);
            logger.LogError(exception, "Company {SupplierName} dealer pricing sync run {ImportRunId} failed.", supplierName, nextRun.Id);
            await MarkCompanyRunFailedAsync(dbContext, nextRun.Id, FailureMessage(exception), cancellationToken);
        }
    }

    private static string FailureMessage(Exception exception)
    {
        var baseException = exception.GetBaseException();
        var message = ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
        return Limit(message, 1000);
    }

    private static async Task MarkSupplierRunFailedAsync(IApplicationDbContext dbContext, Guid importRunId, string message, CancellationToken cancellationToken)
    {
        if (dbContext is DbContext efDbContext)
        {
            efDbContext.ChangeTracker.Clear();
        }

        var run = await dbContext.SupplierConnectorImportRuns
            .SingleOrDefaultAsync(x => x.Id == importRunId, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Status = "Failed";
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.Message = Limit(message, 1000);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkCompanyRunFailedAsync(IApplicationDbContext dbContext, Guid importRunId, string message, CancellationToken cancellationToken)
    {
        if (dbContext is DbContext efDbContext)
        {
            efDbContext.ChangeTracker.Clear();
        }

        var run = await dbContext.CompanySupplierConnectorImportRuns
            .SingleOrDefaultAsync(x => x.Id == importRunId, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Status = "Failed";
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.Message = Limit(message, 1000);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Limit(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static async Task<SupplierDealerPricingImportResult> RunCompanyDealerPricingImportAsync(IServiceProvider serviceProvider, CompanySupplierConnectorImportRun nextRun, CancellationToken cancellationToken)
    {
        if (string.Equals(nextRun.ImportType, DealerPricingImportType, StringComparison.OrdinalIgnoreCase))
        {
            var result = await serviceProvider.GetRequiredService<IWpsDealerPricingImportService>()
                .ImportAsync(new WpsDealerPricingImportRequest(nextRun.Id), cancellationToken);
            return new SupplierDealerPricingImportResult(
                result.RowsProcessed,
                result.PricesUpserted,
                result.UnmatchedRows,
                result.PriceFileUrl,
                result.PriceFileLastModifiedUtc,
                result.PriceFileDownloadedAtUtc);
        }

        if (string.Equals(nextRun.ImportType, PartsUnlimitedDealerPricingImportType, StringComparison.OrdinalIgnoreCase))
        {
            return await serviceProvider.GetRequiredService<IPartsUnlimitedDealerPricingImportService>()
                .ImportAsync(new PartsUnlimitedDealerPricingImportRequest(nextRun.Id), cancellationToken);
        }

        if (string.Equals(nextRun.ImportType, Turn14DealerPricingImportType, StringComparison.OrdinalIgnoreCase))
        {
            return await serviceProvider.GetRequiredService<ITurn14DealerPricingImportService>()
                .ImportAsync(new Turn14DealerPricingImportRequest(nextRun.Id), cancellationToken);
        }

        throw new InvalidOperationException($"Unsupported company dealer pricing import type '{nextRun.ImportType}'.");
    }

    private static bool IsDue(DateTimeOffset now, DateTimeOffset? lastScheduledAtUtc, int cadenceMinutes)
    {
        var cadence = TimeSpan.FromMinutes(Math.Max(60, cadenceMinutes));
        return lastScheduledAtUtc is null || now - lastScheduledAtUtc.Value >= cadence;
    }

    private bool DisableWpsMasterFileImports()
    {
        return configuration.GetValue<bool>("SupplierImports:DisableWpsMasterFile");
    }

    private static string CompanyDealerPricingImportType(string connectorKey)
    {
        return connectorKey.ToUpperInvariant() switch
        {
            WpsConnectorKey => DealerPricingImportType,
            PartsUnlimitedConnectorKey => PartsUnlimitedDealerPricingImportType,
            Turn14ConnectorKey => Turn14DealerPricingImportType,
            _ => throw new InvalidOperationException($"Unsupported company supplier connector key '{connectorKey}'.")
        };
    }

    private static string CompanyDealerPricingDisplayName(string value)
    {
        if (string.Equals(value, WpsConnectorKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, DealerPricingImportType, StringComparison.OrdinalIgnoreCase))
        {
            return "WPS";
        }

        if (string.Equals(value, PartsUnlimitedConnectorKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, PartsUnlimitedDealerPricingImportType, StringComparison.OrdinalIgnoreCase))
        {
            return "Parts Unlimited";
        }

        if (string.Equals(value, Turn14ConnectorKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Turn14DealerPricingImportType, StringComparison.OrdinalIgnoreCase))
        {
            return "Turn14";
        }

        return value;
    }

    private static bool IsMissingCompanySupplierTable(Exception exception)
    {
        return exception.ToString().Contains("42P01", StringComparison.Ordinal) &&
            exception.ToString().Contains("company_supplier_", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> HasOpenRunAsync(IApplicationDbContext dbContext, Guid configurationId, string importType, CancellationToken cancellationToken)
    {
        return await dbContext.SupplierConnectorImportRuns.AnyAsync(
            x => x.SupplierConnectorConfigurationId == configurationId &&
                x.ImportType == importType &&
                (x.Status == "Queued" || x.Status == "Running"),
            cancellationToken);
    }

    private static async Task<bool> HasOpenCompanyRunAsync(IApplicationDbContext dbContext, Guid organizationId, Guid configurationId, string importType, CancellationToken cancellationToken)
    {
        return await dbContext.CompanySupplierConnectorImportRuns.IgnoreQueryFilters().AnyAsync(
            x => x.OrganizationId == organizationId &&
                x.CompanySupplierConnectorConfigurationId == configurationId &&
                x.ImportType == importType &&
                (x.Status == "Queued" || x.Status == "Running"),
            cancellationToken);
    }

    private static int? ReadMaxItems(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(parametersJson);
        if (document.RootElement.TryGetProperty("ImportMode", out var importMode) &&
            importMode.ValueKind == JsonValueKind.String &&
            IsUncappedManualImportMode(importMode.GetString()))
        {
            return null;
        }

        return document.RootElement.TryGetProperty("MaxItems", out var maxItems) && maxItems.ValueKind == JsonValueKind.Number
            ? maxItems.GetInt32()
            : null;
    }

    private static bool IsUncappedManualImportMode(string? importMode)
    {
        return string.Equals(importMode, "Full", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(importMode, "Delta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(importMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ReadBrandFileMaxFiles(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(parametersJson);
        if (document.RootElement.TryGetProperty("ImportMode", out var importMode) &&
            importMode.ValueKind == JsonValueKind.String &&
            IsUncappedManualImportMode(importMode.GetString()))
        {
            return null;
        }

        return document.RootElement.TryGetProperty("BrandFileMaxFiles", out var maxFiles) && maxFiles.ValueKind == JsonValueKind.Number
            ? maxFiles.GetInt32()
            : null;
    }

    private sealed record FitmentRunParameters(
        string? SupplierCode,
        string? Sku,
        int? MaxSkus,
        int? FitmentLimit,
        int? DelayMilliseconds,
        string? BaseUrl,
        bool ExcludeNla)
    {
        public static FitmentRunParameters FromJson(string? parametersJson)
        {
            if (string.IsNullOrWhiteSpace(parametersJson))
            {
                return new(null, null, null, null, 250, DefaultFitmentSourceBaseUrl, true);
            }

            using var document = JsonDocument.Parse(parametersJson);
            var root = document.RootElement;
            var importMode = ReadString(root, "ImportMode");
            return new FitmentRunParameters(
                ReadString(root, "SupplierCode"),
                ReadString(root, "Sku"),
                IsUncappedManualImportMode(importMode) ? null : ReadInt(root, "MaxSkus"),
                ReadInt(root, "FitmentLimit"),
                ReadInt(root, "DelayMilliseconds"),
                ReadString(root, "BaseUrl"),
                ReadBool(root, "ExcludeNla") ?? true);
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int? ReadInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : null;
        }

        private static bool? ReadBool(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
        }
    }

    private sealed record MediaEnrichmentRunParameters(
        int? MaxItems,
        int? DelayMilliseconds)
    {
        public static MediaEnrichmentRunParameters FromJson(string? parametersJson)
        {
            if (string.IsNullOrWhiteSpace(parametersJson))
            {
                return new(null, 750);
            }

            using var document = JsonDocument.Parse(parametersJson);
            var root = document.RootElement;
            var importMode = root.TryGetProperty("ImportMode", out var importModeElement) && importModeElement.ValueKind == JsonValueKind.String
                ? importModeElement.GetString()
                : null;
            return new MediaEnrichmentRunParameters(
                IsUncappedManualImportMode(importMode) ? null : ReadInt(root, "MaxItems"),
                ReadInt(root, "DelayMilliseconds"));
        }

        private static int? ReadInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : null;
        }
    }

    private sealed record PartsUnlimitedConnectorOptions(
        bool ImportBrandFilesWithBundle,
        string? BrandFileUrls,
        int? BrandFileMaxFiles)
    {
        public static PartsUnlimitedConnectorOptions FromConfiguration(SupplierConnectorConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.ApiSecretProtected))
            {
                return new PartsUnlimitedConnectorOptions(false, null, null);
            }

            try
            {
                return JsonSerializer.Deserialize<PartsUnlimitedConnectorOptions>(configuration.ApiSecretProtected) ??
                    new PartsUnlimitedConnectorOptions(false, null, null);
            }
            catch (JsonException)
            {
                return new PartsUnlimitedConnectorOptions(false, null, null);
            }
        }
    }
}
