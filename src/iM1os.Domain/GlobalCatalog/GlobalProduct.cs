using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class GlobalProduct : AuditableEntity
{
    public required string Brand { get; set; }

    public string? Manufacturer { get; set; }

    public string? ManufacturerPartNumber { get; set; }

    public string? NormalizedManufacturerPartNumber { get; set; }

    public required string Description { get; set; }

    public string? LongDescription { get; set; }

    public string? Category { get; set; }

    public string? Upc { get; set; }

    public decimal? Length { get; set; }

    public decimal? Width { get; set; }

    public decimal? Height { get; set; }

    public decimal? Weight { get; set; }

    public string? ImagesJson { get; set; }

    public string? SpecificationsJson { get; set; }

    public required string Status { get; set; }

    public bool IsActive { get; set; } = true;
}
