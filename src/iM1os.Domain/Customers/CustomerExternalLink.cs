using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class CustomerExternalLink : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public required string Provider { get; set; }

    public required string ExternalCustomerId { get; set; }

    public string? ExternalUrl { get; set; }

    public string? MetadataJson { get; set; }

    public bool IsActive { get; set; } = true;
}
