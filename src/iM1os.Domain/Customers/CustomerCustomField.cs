using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerCustomField : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public required string FieldKey { get; set; }

    public string? FieldLabel { get; set; }

    public string? FieldValue { get; set; }
}
