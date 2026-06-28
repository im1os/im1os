using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerAddress : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string AddressType { get; set; } = "Primary";

    public string? Line1 { get; set; }

    public string? Line2 { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = "US";

    public bool IsPrimary { get; set; }

    public bool IsBilling { get; set; }

    public bool IsShipping { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
