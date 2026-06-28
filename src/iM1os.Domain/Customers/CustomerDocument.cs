using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerDocument : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public required string FileName { get; set; }

    public string DocumentType { get; set; } = "General";

    public string? StorageKey { get; set; }

    public string? Url { get; set; }

    public string? ContentType { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
