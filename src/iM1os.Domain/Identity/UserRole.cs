namespace iM1os.Domain.Identity;

public sealed class UserRole
{
    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }
}
