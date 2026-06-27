using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class Permission : Entity
{
    public required string Key { get; set; }

    public required string Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; } = new List<RolePermission>();
}
