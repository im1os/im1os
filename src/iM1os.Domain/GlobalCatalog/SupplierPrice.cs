using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class SupplierPrice : AuditableEntity
{
    public Guid SupplierProductId { get; set; }

    public decimal? Msrp { get; set; }

    public decimal? Map { get; set; }

    public decimal? DealerCost { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}
