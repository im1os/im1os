using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeSafetyIncident : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid? EmployeeId { get; set; }

    public DateOnly IncidentDate { get; set; }

    public string IncidentType { get; set; } = "Incident";

    public string? Severity { get; set; }

    public decimal LostTimeHours { get; set; }

    public bool IsOshaRecordable { get; set; }

    public bool ReportedToOsha { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Employee? Employee { get; set; }
}
