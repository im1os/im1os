using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class Role : AuditableEntity
{
    public Guid? OrganizationId { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public bool IsSystemRole { get; set; }

    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; } = new List<RolePermission>();
}
