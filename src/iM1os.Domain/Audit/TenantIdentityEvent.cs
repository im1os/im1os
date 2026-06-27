using iM1os.Domain.Common;

namespace iM1os.Domain.Audit;

public sealed class TenantIdentityEvent : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid? UserId { get; set; }

    public required string EventType { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? IpAddress { get; set; }

    public string PayloadJson { get; set; } = "{}";
}
