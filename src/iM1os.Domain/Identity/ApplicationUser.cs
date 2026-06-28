using iM1os.Domain.Common;

namespace iM1os.Domain.Identity;

public sealed class ApplicationUser : AuditableEntity
{
    public Guid OrganizationId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string DisplayName { get; set; }

    public string? Phone { get; set; }

    public string? JobTitle { get; set; }

    public required string PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DisabledAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTimeOffset? LastPasswordChangedAtUtc { get; set; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    public int AccessFailedCount { get; set; }

    public DateTimeOffset? LockoutEndAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public bool MfaEnabled { get; set; }

    public string? MfaMethod { get; set; }

    public string? MfaSecretProtected { get; set; }

    public string? AvatarUrl { get; set; }

    public string? PinHash { get; set; }

    public string Language { get; set; } = "en-US";

    public string TimeZone { get; set; } = "America/Chicago";

    public ICollection<OrganizationMembership> OrganizationMemberships { get; } = new List<OrganizationMembership>();

    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();

    public ICollection<UserPermissionOverride> PermissionOverrides { get; } = new List<UserPermissionOverride>();

    public ICollection<UserSession> Sessions { get; } = new List<UserSession>();
}
