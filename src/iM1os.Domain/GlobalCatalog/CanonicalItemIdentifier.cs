using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CanonicalItemIdentifier : AuditableEntity
{
    public Guid CanonicalItemId { get; set; }

    public required string IdentifierType { get; set; }

    public required string IdentifierValue { get; set; }

    public required string NormalizedValue { get; set; }

    public Guid? SupplierId { get; set; }

    public string? SupplierCode { get; set; }

    public Guid? SupplierProductId { get; set; }

    public string? Source { get; set; }

    public bool IsPrimary { get; set; }
}
