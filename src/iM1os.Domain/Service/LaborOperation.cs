using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class LaborOperation : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? ServiceCategory { get; set; }

    public decimal? BaseHours { get; set; }

    public bool IsActive { get; set; } = true;
}
