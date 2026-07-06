using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Hardware;

public sealed class PaymentTerminal : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string DeviceType { get; set; } = "Terminal";

    public string Model { get; set; } = string.Empty;

    public string ProviderTerminalId { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? AssignedRegister { get; set; }

    public string? AssignedEmployeeId { get; set; }

    public string? FirmwareVersion { get; set; }

    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
}
