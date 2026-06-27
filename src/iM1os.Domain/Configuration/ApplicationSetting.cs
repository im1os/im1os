using iM1os.Domain.Common;

namespace iM1os.Domain.Configuration;

public sealed class ApplicationSetting : AuditableEntity
{
    public Guid? OrganizationId { get; set; }

    public required string Key { get; set; }

    public required string Value { get; set; }

    public bool IsEncrypted { get; set; }
}
