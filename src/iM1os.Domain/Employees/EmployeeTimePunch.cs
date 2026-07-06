using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeTimePunch : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateTimeOffset ClockInUtc { get; set; }

    public DateTimeOffset? ClockOutUtc { get; set; }

    public decimal? Hours { get; set; }

    public string? Note { get; set; }

    public bool IsManualEntry { get; set; }

    public string Source { get; set; } = "TimeClock";

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Employee? Employee { get; set; }
}
