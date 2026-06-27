using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class PlatformAuditEvent : Entity
{
    public string? ActorPlatformUserId { get; set; }

    public Guid? TargetOrganizationId { get; set; }

    public required string Action { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? PreviousValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string? IpAddress { get; set; }
}
