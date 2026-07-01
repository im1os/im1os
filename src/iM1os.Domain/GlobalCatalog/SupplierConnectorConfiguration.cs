using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class SupplierConnectorConfiguration : AuditableEntity
{
    public Guid SupplierId { get; set; }

    public required string ConnectorKey { get; set; }

    public required string DisplayName { get; set; }

    public string? BaseApiUrl { get; set; }

    public string? MasterFileUrl { get; set; }

    public string? DealerAccountNumber { get; set; }

    public string? Username { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiSecretProtected { get; set; }

    public required string AuthMode { get; set; }

    public bool IsEnabled { get; set; }

    public bool ImportMasterFileOnSchedule { get; set; }

    public string? MasterFileImportMode { get; set; }

    public int MasterFileScheduleCadenceMinutes { get; set; } = 1440;

    public int? MasterFileScheduleMaxItems { get; set; }

    public DateTimeOffset? LastMasterFileScheduledAtUtc { get; set; }

    public bool ImportFitmentOnSchedule { get; set; }

    public int FitmentScheduleCadenceMinutes { get; set; } = 1440;

    public int? FitmentScheduleMaxSkus { get; set; }

    public int? FitmentScheduleFitmentLimit { get; set; }

    public int FitmentScheduleDelayMilliseconds { get; set; } = 250;

    public string? FitmentSourceBaseUrl { get; set; }

    public DateTimeOffset? LastFitmentScheduledAtUtc { get; set; }

    public bool ImportMediaOnSchedule { get; set; }

    public int MediaScheduleCadenceMinutes { get; set; } = 1440;

    public int? MediaScheduleMaxItems { get; set; }

    public int MediaScheduleDelayMilliseconds { get; set; } = 750;

    public DateTimeOffset? LastMediaScheduledAtUtc { get; set; }

    public DateTimeOffset? LastConnectionTestAtUtc { get; set; }

    public string? LastConnectionStatus { get; set; }

    public string? LastConnectionMessage { get; set; }
}
