using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class ProductMatchReviewItem : AuditableEntity
{
    public Guid SupplierId { get; set; }

    public required string SupplierSku { get; set; }

    public string? SupplierPartNumber { get; set; }

    public string? Upc { get; set; }

    public string? Brand { get; set; }

    public string? SupplierDescription { get; set; }

    public Guid? CandidateGlobalProductId { get; set; }

    public required string MatchReason { get; set; }

    public required string Status { get; set; }
}
