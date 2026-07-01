using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class WorkOrder : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public required string WorkOrderNumber { get; set; }

    public string? RepairOrderNumber { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? CustomerVehicleId { get; set; }

    public Guid? ServiceAdvisorEmployeeId { get; set; }

    public required string Stage { get; set; }

    public required string Priority { get; set; }

    public DateOnly? PromiseDate { get; set; }

    public DateOnly? IntakeDate { get; set; }

    public string? RequestedService { get; set; }

    public string? CustomerConcern { get; set; }

    public string? DiagnosisFindings { get; set; }

    public string? ServiceNotes { get; set; }

    public string? PartsAndSuppliesNotes { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public ICollection<WorkOrderTechnicianAssignment> TechnicianAssignments { get; } = new List<WorkOrderTechnicianAssignment>();

    public ICollection<Estimate> Estimates { get; } = new List<Estimate>();
}
