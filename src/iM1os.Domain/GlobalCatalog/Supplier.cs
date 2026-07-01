using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class Supplier : AuditableEntity
{
    public required string Name { get; set; }

    public required string Code { get; set; }

    public string? ConnectorKey { get; set; }

    public bool IsActive { get; set; } = true;
}
