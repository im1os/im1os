using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class PlatformEvent : Entity
{
    public Guid? TargetOrganizationId { get; set; }

    public string? ActorPlatformUserId { get; set; }

    public required string EventType { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string PayloadJson { get; set; }

    public required string CorrelationId { get; set; }
}
