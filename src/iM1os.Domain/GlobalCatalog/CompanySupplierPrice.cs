using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CompanySupplierPrice : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid SupplierId { get; set; }

    public Guid SupplierProductId { get; set; }

    public required string SupplierSku { get; set; }

    public string? SourceSupplierProductId { get; set; }

    public decimal ActualDealerCost { get; set; }

    public string Currency { get; set; } = "USD";

    public DateOnly? EffectiveDate { get; set; }

    public DateTimeOffset LastSyncedAtUtc { get; set; }

    public string? SourceDataJson { get; set; }
}
