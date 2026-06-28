using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class Customer : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public required string DisplayName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? CompanyName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string CustomerType { get; set; } = "Individual";

    public string Status { get; set; } = "Active";

    public string? LifecycleStage { get; set; }

    public string? Source { get; set; }

    public string? PreferredContactMethod { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CustomerAddress> Addresses { get; } = new List<CustomerAddress>();

    public ICollection<CustomerPhoneNumber> PhoneNumbers { get; } = new List<CustomerPhoneNumber>();

    public ICollection<CustomerNote> Notes { get; } = new List<CustomerNote>();

    public ICollection<CustomerTag> Tags { get; } = new List<CustomerTag>();

    public ICollection<CustomerCustomField> CustomFields { get; } = new List<CustomerCustomField>();

    public ICollection<CustomerExternalLink> ExternalLinks { get; } = new List<CustomerExternalLink>();

    public ICollection<CustomerDocument> Documents { get; } = new List<CustomerDocument>();
}
