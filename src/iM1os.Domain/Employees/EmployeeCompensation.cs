using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeCompensation : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public string PayrollType { get; set; } = "Hourly";

    public decimal? HourlyRate { get; set; }

    public decimal? SalaryAmount { get; set; }

    public decimal? WorkOrderCommissionRate { get; set; }

    public decimal? SalesCommissionRate { get; set; }

    public DateOnly EffectiveStartDate { get; set; }

    public DateOnly? EffectiveEndDate { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
