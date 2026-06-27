using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class TenantModuleEntitlement : Entity
{
    public Guid OrganizationId { get; set; }

    public required string ModuleKey { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset EnabledAtUtc { get; set; }

    public string? EnabledByPlatformUserId { get; set; }
}
