using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerTag : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public required string Tag { get; set; }
}
