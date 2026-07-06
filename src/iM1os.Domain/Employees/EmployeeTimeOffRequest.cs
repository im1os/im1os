using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeTimeOffRequest : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public string Type { get; set; } = "PTO";

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal HoursPerDay { get; set; }

    public string Status { get; set; } = "Pending";

    public string? Note { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public string? ReviewedByUserId { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Employee? Employee { get; set; }
}
