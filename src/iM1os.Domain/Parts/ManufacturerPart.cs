using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class ManufacturerPart : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public required string ManufacturerPartNumber { get; set; }

    public required string Brand { get; set; }

    public required string Description { get; set; }

    public string? Upc { get; set; }

    public string? Category { get; set; }

    public string? Subcategory { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Length { get; set; }

    public decimal? Width { get; set; }

    public decimal? Height { get; set; }

    public decimal? Msrp { get; set; }

    public decimal? Map { get; set; }

    public required string Status { get; set; }

    public Guid? SupersededByManufacturerPartId { get; set; }

    public bool IsActive { get; set; } = true;
}
