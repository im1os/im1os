using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class PlatformUser : AuditableEntity
{
    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public required string Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
