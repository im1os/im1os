using iM1os.Domain.Common;

namespace iM1os.Domain.Audit;

public sealed class TimelineEvent : Entity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public required string EntityType { get; set; }

    public required string EntityId { get; set; }

    public required string EventType { get; set; }

    public string? ActorUserId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string Summary { get; set; }

    public string? PayloadJson { get; set; }
}
