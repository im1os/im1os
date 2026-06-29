using iM1os.Domain.Common;

namespace iM1os.Domain.Vehicles;

public sealed class CustomerVehicleAttachment : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid CustomerVehicleId { get; set; }

    public CustomerVehicle? CustomerVehicle { get; set; }

    public string AttachmentType { get; set; } = "Photo";

    public required string FileName { get; set; }

    public string? Url { get; set; }

    public string? ContentType { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
