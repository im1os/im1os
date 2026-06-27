using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class WorkOrder : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public required string WorkOrderNumber { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? CustomerVehicleId { get; set; }

    public required string Stage { get; set; }

    public required string Priority { get; set; }

    public string? RequestedService { get; set; }

    public string? CustomerConcern { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }
}
