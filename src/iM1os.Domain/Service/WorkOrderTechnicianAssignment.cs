using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class WorkOrderTechnicianAssignment : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid WorkOrderId { get; set; }

    public Guid EmployeeId { get; set; }

    public string Role { get; set; } = "Technician";

    public decimal SplitPercent { get; set; }

    public int SortOrder { get; set; }
}
