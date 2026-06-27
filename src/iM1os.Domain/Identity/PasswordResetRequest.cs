using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class PasswordResetRequest : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
