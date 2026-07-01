using iM1os.Domain.Common;

namespace iM1os.Domain.Service;

public sealed class WorkOrderAttachment : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? CustomerVehicleId { get; set; }

    public string AttachmentType { get; set; } = "Photo";

    public required string FileName { get; set; }

    public string? Url { get; set; }

    public string? ContentType { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
