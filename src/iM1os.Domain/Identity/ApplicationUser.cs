using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class ApplicationUser : AuditableEntity
{
    public Guid OrganizationId { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public ICollection<OrganizationMembership> OrganizationMemberships { get; } = new List<OrganizationMembership>();

    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();
}
