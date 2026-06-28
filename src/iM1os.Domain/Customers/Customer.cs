using iM1os.Domain.Common;

namespace iM1os.Domain.Customers;

public sealed class Customer : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public required string DisplayName { get; set; }

    public string? CustomerNumber { get; set; }

    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? Nickname { get; set; }

    public string? CompanyName { get; set; }

    public string? Email { get; set; }

    public string? SecondaryEmail { get; set; }

    public string? Phone { get; set; }

    public string? MobilePhone { get; set; }

    public string? HomePhone { get; set; }

    public string? WorkPhone { get; set; }

    public string CustomerType { get; set; } = "Individual";

    public string Status { get; set; } = "Active";

    public string? LifecycleStage { get; set; }

    public string? Source { get; set; }

    public string? PreferredContactMethod { get; set; }

    public bool AllowEmailMarketing { get; set; } = true;

    public bool AllowSmsMarketing { get; set; } = true;

    public bool AllowPhoneCalls { get; set; } = true;

    public bool TaxExempt { get; set; }

    public string? TaxExemptNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly? Anniversary { get; set; }

    public string? PreferredLanguage { get; set; }

    public DateOnly? CustomerSince { get; set; }

    public DateTimeOffset? LastPurchaseAtUtc { get; set; }

    public decimal LifetimeSales { get; set; }

    public decimal? CreditLimit { get; set; }

    public decimal CurrentBalance { get; set; }

    public decimal StoreCredit { get; set; }

    public string? SummaryNotes { get; set; }

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
