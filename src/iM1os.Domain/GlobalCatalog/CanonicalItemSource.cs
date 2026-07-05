using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CanonicalItemSource : AuditableEntity
{
    public Guid CanonicalItemId { get; set; }

    public Guid? GlobalProductId { get; set; }

    public Guid? SupplierId { get; set; }

    public Guid? SupplierProductId { get; set; }

    public string? SupplierCode { get; set; }

    public required string SourceTable { get; set; }

    public required string SourceKey { get; set; }

    public required string MatchMethod { get; set; }

    public decimal? MatchConfidence { get; set; }
}
