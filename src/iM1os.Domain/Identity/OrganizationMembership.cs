using iM1os.Domain.Common;
using iM1os.Domain.Tenancy;

namespace iM1os.Domain.Identity;

public sealed class OrganizationMembership : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public required string DisplayName { get; set; }

    public string? EmployeeNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
