using iM1os.Domain.Common;

namespace iM1os.Domain.Tenancy;

public sealed class Location : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public required string Name { get; set; }

    public required string Code { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public bool IsActive { get; set; } = true;
}
