using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CanonicalItem : AuditableEntity
{
    public string? Brand { get; set; }

    public string? Manufacturer { get; set; }

    public string? ManufacturerPartNumber { get; set; }

    public string? NormalizedManufacturerPartNumber { get; set; }

    public required string Title { get; set; }

    public string? Category { get; set; }

    public string? Subcategory { get; set; }

    public string? PrimaryUpc { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public string? SearchText { get; set; }

    public required string Status { get; set; }

    public bool IsActive { get; set; } = true;
}
