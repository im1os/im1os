using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class UserPermissionOverride : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public Guid PermissionId { get; set; }

    public Permission? Permission { get; set; }

    public bool IsAllowed { get; set; }
}
