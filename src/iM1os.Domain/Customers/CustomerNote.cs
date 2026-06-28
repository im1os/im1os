using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerNote : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? AuthorDisplayName { get; set; }

    public string NoteType { get; set; } = "General";

    public string? Subject { get; set; }

    public required string Body { get; set; }

    public bool IsPinned { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
