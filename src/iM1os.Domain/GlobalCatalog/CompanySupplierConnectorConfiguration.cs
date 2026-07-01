using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CompanySupplierConnectorConfiguration : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid SupplierId { get; set; }

    public required string ConnectorKey { get; set; }

    public required string DisplayName { get; set; }

    public string? BaseApiUrl { get; set; }

    public string? DealerAccountNumber { get; set; }

    public string? Username { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiSecretProtected { get; set; }

    public required string AuthMode { get; set; }

    public bool IsEnabled { get; set; }

    public bool SyncDealerPricingOnSchedule { get; set; }

    public int DealerPricingScheduleIntervalMinutes { get; set; } = 1440;

    public int? DealerPricingScheduleMaxItems { get; set; }

    public DateTimeOffset? LastDealerPricingScheduledAtUtc { get; set; }

    public DateTimeOffset? LastConnectionTestAtUtc { get; set; }

    public string? LastConnectionStatus { get; set; }

    public string? LastConnectionMessage { get; set; }
}
