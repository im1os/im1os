using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Application.Platform;
using iM1os.Domain.Audit;
using iM1os.Domain.GlobalCatalog;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class CompanySupplierService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ITenantModuleEntitlementService tenantModuleEntitlements) : ICompanySupplierService
{
    private const string WpsSupplierCode = "WPS";
    private const string WpsSupplierName = "Western Power Sports";
    private const string WpsConnectorKey = "WPS";
    private const string DefaultBaseApiUrl = "https://api.wps-inc.com";
    private const string DealerPricingImportType = "WpsDealerPricing";
    private const string PartsUnlimitedSupplierCode = "PU";
    private const string PartsUnlimitedSupplierName = "Parts Unlimited";
    private const string PartsUnlimitedConnectorKey = "PU";
    private const string PartsUnlimitedDefaultBaseApiUrl = "https://api.parts-unlimited.com/api";
    private const string PartsUnlimitedDealerPricingImportType = "PartsUnlimitedDealerPricing";
    private const string Turn14SupplierCode = "TURN14";
    private const string Turn14SupplierName = "Turn14";
    private const string Turn14ConnectorKey = "TURN14";
    private const string Turn14DefaultBaseApiUrl = "https://api.turn14.com";
    private const string Turn14DealerPricingImportType = "Turn14DealerPricing";

    public async Task<CompanyWpsConnectorPage> GetWpsConnectorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureSupplierConnectorEnabledAsync(organizationId, WpsSupplierCode, cancellationToken);
        var configuration = await GetOrCreateWpsConfigurationAsync(organizationId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(organizationId, configuration.Id, cancellationToken);
    }

    public async Task<CompanyWpsConnectorPage> SaveWpsConnectorAsync(Guid organizationId, Guid userId, CompanyWpsConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureSupplierConnectorEnabledAsync(organizationId, WpsSupplierCode, cancellationToken);
        var configuration = await GetOrCreateWpsConfigurationAsync(organizationId, cancellationToken);
        var before = ToSettings(configuration);

        configuration.IsEnabled = request.IsEnabled;
        configuration.BaseApiUrl = Clean(request.BaseApiUrl) ?? DefaultBaseApiUrl;
        configuration.DealerAccountNumber = Clean(request.DealerAccountNumber);
        configuration.Username = Clean(request.Username);
        configuration.ApiKey = Clean(request.ApiKey);
        if (!string.IsNullOrWhiteSpace(request.ApiSecret))
        {
            configuration.ApiSecretProtected = request.ApiSecret.Trim();
        }

        configuration.AuthMode = "API Key";
        configuration.SyncDealerPricingOnSchedule = request.SyncDealerPricingOnSchedule;
        configuration.DealerPricingScheduleIntervalMinutes = HoursToMinutes(request.DealerPricingScheduleIntervalHours);
        configuration.DealerPricingScheduleMaxItems = PositiveOrNull(request.DealerPricingScheduleMaxItems);
        configuration.LastConnectionStatus = IsConfigured(configuration) ? "Ready" : "Incomplete";
        configuration.LastConnectionMessage = IsConfigured(configuration)
            ? "Company WPS pricing settings are complete."
            : "API key is required before dealer pricing can sync.";

        await RecordCompanySupplierChangeAsync(
            organizationId,
            userId,
            "CompanyWpsConnectorUpdated",
            configuration.Id.ToString(),
            before,
            ToSettings(configuration),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(organizationId, configuration.Id, cancellationToken);
    }

    public async Task<CompanyWpsConnectorPage> QueueWpsDealerPricingSyncAsync(Guid organizationId, Guid userId, CompanyWpsDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureSupplierConnectorEnabledAsync(organizationId, WpsSupplierCode, cancellationToken);
        var configuration = await GetOrCreateWpsConfigurationAsync(organizationId, cancellationToken);
        if (!configuration.IsEnabled || !IsConfigured(configuration))
        {
            throw new InvalidOperationException("Enable WPS and provide an API key before syncing dealer pricing.");
        }

        var importRun = new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = DealerPricingImportType,
            Status = "Queued",
            RequestedAtUtc = dateTimeProvider.UtcNow,
            RequestedByUserId = userId.ToString(),
            Source = WpsConnectorKey,
            ProgressProcessed = 0,
            ProgressTotal = PositiveOrNull(request.MaxItems),
            ParametersJson = JsonSerializer.Serialize(new { MaxItems = PositiveOrNull(request.MaxItems) }),
            Message = request.MaxItems is > 0
                ? $"WPS dealer pricing sync queued for first {request.MaxItems.Value} items."
                : "WPS dealer pricing sync queued."
        };
        dbContext.CompanySupplierConnectorImportRuns.Add(importRun);

        await RecordCompanySupplierChangeAsync(
            organizationId,
            userId,
            "CompanyWpsDealerPricingSyncQueued",
            configuration.Id.ToString(),
            null,
            new { importRun.Id, importRun.Message },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildPageAsync(organizationId, configuration.Id, cancellationToken);
    }

    public async Task<CompanySupplierConnectorPage> GetPartsUnlimitedConnectorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        return await GetConnectorAsync(organizationId, userId, PartsUnlimitedDefinition(), cancellationToken);
    }

    public async Task<CompanySupplierConnectorPage> SavePartsUnlimitedConnectorAsync(Guid organizationId, Guid userId, CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        return await SaveConnectorAsync(organizationId, userId, PartsUnlimitedDefinition(), request, cancellationToken);
    }

    public async Task<CompanySupplierConnectorPage> QueuePartsUnlimitedDealerPricingSyncAsync(Guid organizationId, Guid userId, CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        return await QueueDealerPricingSyncAsync(organizationId, userId, PartsUnlimitedDefinition(), request, cancellationToken);
    }

    public async Task<CompanySupplierConnectorPage> GetTurn14ConnectorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        return await GetConnectorAsync(organizationId, userId, Turn14Definition(), cancellationToken);
    }

    public async Task<CompanySupplierConnectorPage> SaveTurn14ConnectorAsync(Guid organizationId, Guid userId, CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        return await SaveConnectorAsync(organizationId, userId, Turn14Definition(), request, cancellationToken);
    }

    public async Task<CompanySupplierConnectorPage> QueueTurn14DealerPricingSyncAsync(Guid organizationId, Guid userId, CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        return await QueueDealerPricingSyncAsync(organizationId, userId, Turn14Definition(), request, cancellationToken);
    }

    private async Task<CompanySupplierConnectorPage> GetConnectorAsync(Guid organizationId, Guid userId, CompanySupplierConnectorDefinition definition, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureSupplierConnectorEnabledAsync(organizationId, definition.SupplierCode, cancellationToken);
        var configuration = await GetOrCreateConfigurationAsync(organizationId, definition, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildConnectorPageAsync(organizationId, configuration.Id, definition, cancellationToken);
    }

    private async Task<CompanySupplierConnectorPage> SaveConnectorAsync(Guid organizationId, Guid userId, CompanySupplierConnectorDefinition definition, CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureSupplierConnectorEnabledAsync(organizationId, definition.SupplierCode, cancellationToken);
        var configuration = await GetOrCreateConfigurationAsync(organizationId, definition, cancellationToken);
        var before = ToGenericSettings(configuration, definition);

        configuration.IsEnabled = request.IsEnabled;
        configuration.BaseApiUrl = Clean(request.BaseApiUrl) ?? definition.DefaultBaseApiUrl;
        configuration.DealerAccountNumber = Clean(request.DealerAccountNumber);
        configuration.Username = Clean(request.Username);
        configuration.ApiKey = Clean(request.ApiKey);
        if (!string.IsNullOrWhiteSpace(request.ApiSecret))
        {
            configuration.ApiSecretProtected = request.ApiSecret.Trim();
        }

        configuration.AuthMode = definition.AuthMode;
        configuration.SyncDealerPricingOnSchedule = request.SyncDealerPricingOnSchedule;
        configuration.DealerPricingScheduleIntervalMinutes = HoursToMinutes(request.DealerPricingScheduleIntervalHours);
        configuration.DealerPricingScheduleMaxItems = PositiveOrNull(request.DealerPricingScheduleMaxItems);
        configuration.LastConnectionStatus = IsConfigured(configuration, definition) ? "Ready" : "Incomplete";
        configuration.LastConnectionMessage = IsConfigured(configuration, definition)
            ? $"Company {definition.Title} pricing settings are complete."
            : definition.IncompleteMessage;

        await RecordCompanySupplierChangeAsync(
            organizationId,
            userId,
            $"Company{definition.EventName}ConnectorUpdated",
            configuration.Id.ToString(),
            before,
            ToGenericSettings(configuration, definition),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildConnectorPageAsync(organizationId, configuration.Id, definition, cancellationToken);
    }

    private async Task<CompanySupplierConnectorPage> QueueDealerPricingSyncAsync(Guid organizationId, Guid userId, CompanySupplierConnectorDefinition definition, CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken)
    {
        await EnsureCompanyAdministratorAsync(organizationId, userId, cancellationToken);
        await EnsureSupplierConnectorEnabledAsync(organizationId, definition.SupplierCode, cancellationToken);
        var configuration = await GetOrCreateConfigurationAsync(organizationId, definition, cancellationToken);
        if (!configuration.IsEnabled || !IsConfigured(configuration, definition))
        {
            throw new InvalidOperationException($"Enable {definition.Title} and provide required credentials before syncing dealer pricing.");
        }

        var maxItems = PositiveOrNull(request.MaxItems);
        var importRun = new CompanySupplierConnectorImportRun
        {
            OrganizationId = organizationId,
            CompanySupplierConnectorConfigurationId = configuration.Id,
            ImportType = definition.ImportType,
            Status = "Queued",
            RequestedAtUtc = dateTimeProvider.UtcNow,
            RequestedByUserId = userId.ToString(),
            Source = definition.ConnectorKey,
            ProgressProcessed = 0,
            ProgressTotal = maxItems,
            ParametersJson = JsonSerializer.Serialize(new { MaxItems = maxItems }),
            Message = maxItems is > 0
                ? $"{definition.Title} dealer pricing sync queued for first {maxItems.Value} items."
                : $"{definition.Title} dealer pricing sync queued."
        };
        dbContext.CompanySupplierConnectorImportRuns.Add(importRun);

        await RecordCompanySupplierChangeAsync(
            organizationId,
            userId,
            $"Company{definition.EventName}DealerPricingSyncQueued",
            configuration.Id.ToString(),
            null,
            new { importRun.Id, importRun.Message },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildConnectorPageAsync(organizationId, configuration.Id, definition, cancellationToken);
    }

    private async Task<CompanyWpsConnectorPage> BuildPageAsync(Guid organizationId, Guid configurationId, CancellationToken cancellationToken)
    {
        var configuration = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == configurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);
        var runRows = await dbContext.CompanySupplierConnectorImportRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CompanySupplierConnectorConfigurationId == configuration.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        var runs = runRows
            .Select(x => new CompanySupplierImportRunRow(
                x.Id,
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.Message,
                ImportProgressPercent(x.Status, x.ProgressProcessed, x.ProgressTotal),
                x.ProgressProcessed,
                x.ProgressTotal,
                x.ParametersJson))
            .ToList();

        return new CompanyWpsConnectorPage(
            organizationId,
            ToSettings(configuration),
            new CompanyWpsConnectorStatus(
                configuration.Id,
                supplier.Id,
                supplier.Name,
                configuration.ConnectorKey,
                IsConfigured(configuration),
                configuration.IsEnabled,
                configuration.SyncDealerPricingOnSchedule,
                configuration.LastDealerPricingScheduledAtUtc,
                configuration.LastConnectionTestAtUtc,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage),
            PriceFileStatus(runs),
            runs);
    }

    private async Task<CompanySupplierConnectorPage> BuildConnectorPageAsync(Guid organizationId, Guid configurationId, CompanySupplierConnectorDefinition definition, CancellationToken cancellationToken)
    {
        var configuration = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == configurationId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleAsync(x => x.Id == configuration.SupplierId, cancellationToken);
        var runRows = await dbContext.CompanySupplierConnectorImportRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.CompanySupplierConnectorConfigurationId == configuration.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        var runs = runRows
            .Select(x => new CompanySupplierImportRunRow(
                x.Id,
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.Message,
                ImportProgressPercent(x.Status, x.ProgressProcessed, x.ProgressTotal),
                x.ProgressProcessed,
                x.ProgressTotal,
                x.ParametersJson))
            .ToList();

        return new CompanySupplierConnectorPage(
            organizationId,
            supplier.Code,
            supplier.Name,
            definition.Title,
            definition.Description,
            definition.PricingDescription,
            definition.SaveAction,
            definition.SyncAction,
            definition.ApiKeyLabel,
            definition.ApiSecretLabel,
            definition.BaseApiUrlPlaceholder,
            ToGenericSettings(configuration, definition),
            new CompanySupplierConnectorStatus(
                configuration.Id,
                supplier.Id,
                supplier.Name,
                configuration.ConnectorKey,
                IsConfigured(configuration, definition),
                configuration.IsEnabled,
                configuration.SyncDealerPricingOnSchedule,
                configuration.LastDealerPricingScheduledAtUtc,
                configuration.LastConnectionTestAtUtc,
                configuration.LastConnectionStatus,
                configuration.LastConnectionMessage),
            PriceSyncStatus(runs, definition),
            runs);
    }

    private async Task<CompanySupplierConnectorConfiguration> GetOrCreateWpsConfigurationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Code == WpsSupplierCode, cancellationToken);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = WpsSupplierName,
                Code = WpsSupplierCode,
                ConnectorKey = WpsConnectorKey,
                IsActive = true
            };
            dbContext.Suppliers.Add(supplier);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var configuration = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x =>
                x.OrganizationId == organizationId &&
                x.SupplierId == supplier.Id &&
                x.ConnectorKey == WpsConnectorKey,
                cancellationToken);
        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = WpsConnectorKey,
            DisplayName = "WPS",
            BaseApiUrl = DefaultBaseApiUrl,
            AuthMode = "API Key",
            IsEnabled = false,
            DealerPricingScheduleIntervalMinutes = 1440,
            LastConnectionStatus = "Incomplete",
            LastConnectionMessage = "API key is required before dealer pricing can sync."
        };
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        return configuration;
    }

    private async Task<CompanySupplierConnectorConfiguration> GetOrCreateConfigurationAsync(Guid organizationId, CompanySupplierConnectorDefinition definition, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Code == definition.SupplierCode, cancellationToken);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                Name = definition.SupplierName,
                Code = definition.SupplierCode,
                ConnectorKey = definition.ConnectorKey,
                IsActive = true
            };
            dbContext.Suppliers.Add(supplier);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var configuration = await dbContext.CompanySupplierConnectorConfigurations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x =>
                x.OrganizationId == organizationId &&
                x.SupplierId == supplier.Id &&
                x.ConnectorKey == definition.ConnectorKey,
                cancellationToken);
        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new CompanySupplierConnectorConfiguration
        {
            OrganizationId = organizationId,
            SupplierId = supplier.Id,
            ConnectorKey = definition.ConnectorKey,
            DisplayName = definition.Title,
            BaseApiUrl = definition.DefaultBaseApiUrl,
            AuthMode = definition.AuthMode,
            IsEnabled = false,
            DealerPricingScheduleIntervalMinutes = 1440,
            LastConnectionStatus = "Incomplete",
            LastConnectionMessage = definition.IncompleteMessage
        };
        dbContext.CompanySupplierConnectorConfigurations.Add(configuration);
        return configuration;
    }

    private async Task EnsureCompanyAdministratorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var isCompanyAdministrator = await dbContext.UserRoles.IgnoreQueryFilters()
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Role != null &&
                x.Role.OrganizationId == organizationId &&
                (x.Role.NormalizedName == "OWNER" || x.Role.NormalizedName == "ADMINISTRATOR"),
                cancellationToken);
        if (isCompanyAdministrator)
        {
            return;
        }

        var isPlatformAdministrator = await dbContext.PlatformUsers
            .AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "Platform Administrator", cancellationToken);
        if (isPlatformAdministrator)
        {
            return;
        }

        throw new UnauthorizedAccessException("Only company owners, company administrators, or platform administrators can manage company suppliers.");
    }

    private async Task EnsureSupplierConnectorEnabledAsync(Guid organizationId, string supplierCode, CancellationToken cancellationToken)
    {
        if (await tenantModuleEntitlements.IsSupplierConnectorEnabledAsync(organizationId, supplierCode, cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException($"{supplierCode} is not enabled for this company.");
    }

    private async Task RecordCompanySupplierChangeAsync(Guid organizationId, Guid userId, string action, string entityId, object? before, object? after, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var changes = JsonSerializer.Serialize(new { before, after });
        dbContext.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = userId.ToString(),
            Action = action,
            EntityName = "CompanySupplierConnectorConfiguration",
            EntityId = entityId,
            ChangesJson = changes,
            OccurredAtUtc = now
        });
        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            EntityType = "CompanySupplierConnectorConfiguration",
            EntityId = entityId,
            EventType = action,
            ActorUserId = userId.ToString(),
            OccurredAtUtc = now,
            Summary = action,
            PayloadJson = changes
        });
        await Task.CompletedTask;
    }

    private static CompanyWpsConnectorSettingsRequest ToSettings(CompanySupplierConnectorConfiguration configuration) => new(
        configuration.IsEnabled,
        configuration.BaseApiUrl ?? DefaultBaseApiUrl,
        configuration.DealerAccountNumber ?? string.Empty,
        configuration.Username ?? string.Empty,
        configuration.ApiKey ?? string.Empty,
        string.Empty,
        configuration.SyncDealerPricingOnSchedule,
        MinutesToHours(configuration.DealerPricingScheduleIntervalMinutes),
        configuration.DealerPricingScheduleMaxItems);

    private static CompanySupplierConnectorSettingsRequest ToGenericSettings(CompanySupplierConnectorConfiguration configuration, CompanySupplierConnectorDefinition definition) => new(
        configuration.IsEnabled,
        configuration.BaseApiUrl ?? definition.DefaultBaseApiUrl,
        configuration.DealerAccountNumber ?? string.Empty,
        configuration.Username ?? string.Empty,
        configuration.ApiKey ?? string.Empty,
        string.Empty,
        configuration.SyncDealerPricingOnSchedule,
        MinutesToHours(configuration.DealerPricingScheduleIntervalMinutes),
        configuration.DealerPricingScheduleMaxItems);

    private static bool IsConfigured(CompanySupplierConnectorConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration.ApiKey);
    }

    private static bool IsConfigured(CompanySupplierConnectorConfiguration configuration, CompanySupplierConnectorDefinition definition)
    {
        return definition.RequiresApiSecret
            ? !string.IsNullOrWhiteSpace(configuration.ApiKey) && !string.IsNullOrWhiteSpace(configuration.ApiSecretProtected)
            : !string.IsNullOrWhiteSpace(configuration.ApiKey);
    }

    private static int ClampPositive(int value, int min, int max)
    {
        return Math.Clamp(value <= 0 ? min : value, min, max);
    }

    private static int HoursToMinutes(int value)
    {
        return ClampPositive(value, 1, 168) * 60;
    }

    private static int MinutesToHours(int value)
    {
        return Math.Max(1, (int)Math.Ceiling((value <= 0 ? 1440 : value) / 60m));
    }

    private static int ImportProgressPercent(string status, int processed, int? total)
    {
        if (total is > 0)
        {
            return Math.Clamp((int)Math.Floor(processed * 100m / total.Value), 0, 100);
        }

        return status switch
        {
            "Queued" => 10,
            "Running" => 50,
            "Completed" => 100,
            "Failed" => 100,
            "Blocked" => 100,
            _ => 0
        };
    }

    private static CompanyWpsPriceFileStatus PriceFileStatus(IReadOnlyCollection<CompanySupplierImportRunRow> runs)
    {
        var lastSuccessfulRun = runs
            .Where(x => string.Equals(x.ImportType, DealerPricingImportType, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefault();
        if (lastSuccessfulRun is null)
        {
            return new CompanyWpsPriceFileStatus(null, null, null, null, "No WPS dealer price file has been applied.");
        }

        var metadata = PriceFileMetadata.FromJson(lastSuccessfulRun.ParametersJson);
        return new CompanyWpsPriceFileStatus(
            metadata.PriceFileLastModifiedUtc,
            metadata.PriceFileDownloadedAtUtc,
            lastSuccessfulRun.CompletedAtUtc,
            metadata.PriceFileUrl ?? lastSuccessfulRun.Source,
            lastSuccessfulRun.Message);
    }

    private static CompanySupplierPriceSyncStatus PriceSyncStatus(IReadOnlyCollection<CompanySupplierImportRunRow> runs, CompanySupplierConnectorDefinition definition)
    {
        var lastSuccessfulRun = runs
            .Where(x => string.Equals(x.ImportType, definition.ImportType, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefault();
        if (lastSuccessfulRun is null)
        {
            return new CompanySupplierPriceSyncStatus(null, null, null, null, $"No {definition.Title} dealer pricing sync has been applied.");
        }

        var metadata = PriceFileMetadata.FromJson(lastSuccessfulRun.ParametersJson);
        return new CompanySupplierPriceSyncStatus(
            metadata.PriceFileLastModifiedUtc,
            metadata.PriceFileDownloadedAtUtc,
            lastSuccessfulRun.CompletedAtUtc,
            metadata.PriceFileUrl ?? lastSuccessfulRun.Source,
            lastSuccessfulRun.Message);
    }

    private static CompanySupplierConnectorDefinition PartsUnlimitedDefinition() => new(
        PartsUnlimitedSupplierCode,
        PartsUnlimitedSupplierName,
        PartsUnlimitedConnectorKey,
        "Parts Unlimited",
        "Configure company-owned Parts Unlimited credentials and dealer actual cost sync.",
        "Company-specific sync for Parts Unlimited actual dealer cost. Catalog, fitment, and warehouse inventory remain platform-managed.",
        "SaveSupplierPartsUnlimitedConnector",
        "SyncSupplierPartsUnlimitedDealerPricing",
        PartsUnlimitedDefaultBaseApiUrl,
        "https://api.parts-unlimited.com/api",
        "API Key",
        "API Secret",
        "API Key",
        false,
        PartsUnlimitedDealerPricingImportType,
        "Api key is required before dealer pricing can sync.",
        "PartsUnlimited");

    private static CompanySupplierConnectorDefinition Turn14Definition() => new(
        Turn14SupplierCode,
        Turn14SupplierName,
        Turn14ConnectorKey,
        "Turn14",
        "Configure company-owned Turn14 API credentials and dealer actual cost sync.",
        "Company-specific sync for Turn14 purchase cost. Catalog, fitment, media, and warehouse inventory remain platform-managed.",
        "SaveSupplierTurn14Connector",
        "SyncSupplierTurn14DealerPricing",
        Turn14DefaultBaseApiUrl,
        "https://api.turn14.com",
        "Client ID",
        "Client Secret",
        "OAuth Client Credentials",
        true,
        Turn14DealerPricingImportType,
        "Client id and client secret are required before dealer pricing can sync.",
        "Turn14");

    private sealed record PriceFileMetadata(string? PriceFileUrl, DateTimeOffset? PriceFileLastModifiedUtc, DateTimeOffset? PriceFileDownloadedAtUtc)
    {
        public static PriceFileMetadata FromJson(string? parametersJson)
        {
            if (string.IsNullOrWhiteSpace(parametersJson))
            {
                return new(null, null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(parametersJson);
                var root = document.RootElement;
                return new(
                    ReadString(root, "PriceFileUrl"),
                    ReadDateTimeOffset(root, "PriceFileLastModifiedUtc"),
                    ReadDateTimeOffset(root, "PriceFileDownloadedAtUtc"));
            }
            catch (JsonException)
            {
                return new(null, null, null);
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(value.GetString(), out var parsed)
                ? parsed
                : null;
        }
    }

    private static int? PositiveOrNull(int? value)
    {
        return value is > 0 ? value : null;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record CompanySupplierConnectorDefinition(
        string SupplierCode,
        string SupplierName,
        string ConnectorKey,
        string Title,
        string Description,
        string PricingDescription,
        string SaveAction,
        string SyncAction,
        string DefaultBaseApiUrl,
        string BaseApiUrlPlaceholder,
        string ApiKeyLabel,
        string ApiSecretLabel,
        string AuthMode,
        bool RequiresApiSecret,
        string ImportType,
        string IncompleteMessage,
        string EventName);
}
