using iM1os.Domain.Common;

namespace iM1os.Domain.Employees;

public sealed class EmployeeDocument : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public required string FileName { get; set; }

    public string DocumentType { get; set; } = "General";

    public string? StorageKey { get; set; }

    public string? Url { get; set; }

    public string? ContentType { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
