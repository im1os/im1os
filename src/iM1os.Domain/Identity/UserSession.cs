using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class UserSession : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public required string SessionKey { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
