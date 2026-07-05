using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CanonicalBrandAlias : AuditableEntity
{
    public required string Brand { get; set; }

    public required string NormalizedBrand { get; set; }

    public required string CanonicalBrand { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
