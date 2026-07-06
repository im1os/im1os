using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeCompanyAsset : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? AssetTag { get; set; }

    public string? SerialNumber { get; set; }

    public DateOnly? IssuedDate { get; set; }

    public DateOnly? ReturnedDate { get; set; }

    public string Status { get; set; } = "Issued";

    public string? Note { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Employee? Employee { get; set; }
}
