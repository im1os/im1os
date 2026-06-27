using iM1os.Domain.Common;

namespace iM1os.Domain.Tenancy;

public sealed class Organization : AuditableEntity
{
    public required string Name { get; set; }

    public required string Slug { get; set; }

    public bool IsActive { get; set; } = true;
}
