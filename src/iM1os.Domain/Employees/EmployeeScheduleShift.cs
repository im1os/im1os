using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeScheduleShift : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateOnly ShiftDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Employee? Employee { get; set; }
}
