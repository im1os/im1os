using iM1os.Domain.Common;

namespace iM1os.Domain.Configuration;

public sealed class FeatureFlag : AuditableEntity
{
    public Guid? OrganizationId { get; set; }

    public required string Key { get; set; }

    public bool IsEnabled { get; set; }

    public string? Description { get; set; }
}
