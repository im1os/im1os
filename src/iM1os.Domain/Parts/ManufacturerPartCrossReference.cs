using iM1os.Domain.Common;

namespace iM1os.Domain.Parts;

public sealed class ManufacturerPartCrossReference : Entity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid ManufacturerPartId { get; set; }

    public required string ReferenceType { get; set; }

    public required string ReferenceValue { get; set; }

    public string? Brand { get; set; }

    public string? Notes { get; set; }
}
