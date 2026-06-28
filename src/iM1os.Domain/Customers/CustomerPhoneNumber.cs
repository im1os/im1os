using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerPhoneNumber : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string PhoneType { get; set; } = "Mobile";

    public required string PhoneNumber { get; set; }

    public string? Extension { get; set; }

    public bool IsPrimary { get; set; }

    public bool CanText { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
