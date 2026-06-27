using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class ManufacturerPartImage : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid ManufacturerPartId { get; set; }

    public required string Url { get; set; }

    public string? AltText { get; set; }

    public int SortOrder { get; set; }
}
