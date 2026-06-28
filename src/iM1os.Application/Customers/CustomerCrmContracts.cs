namespace iM1os.Application.Customers;

public sealed record CustomerWorkspace(
    IReadOnlyCollection<CustomerRow> Customers,
    string? Query,
    string? Status);

public sealed record CustomerRow(
    Guid Id,
    string DisplayName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string Status,
    string? LifecycleStage,
    string? Source,
    DateTimeOffset CreatedAtUtc);

public sealed record CustomerDetail(
    Guid Id,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string CustomerType,
    string Status,
    string? LifecycleStage,
    string? Source,
    string? PreferredContactMethod,
    IReadOnlyCollection<CustomerAddressItem> Addresses,
    IReadOnlyCollection<CustomerPhoneItem> PhoneNumbers,
    IReadOnlyCollection<CustomerUnitItem> Units,
    IReadOnlyCollection<CustomerNoteItem> Notes,
    IReadOnlyCollection<CustomerTagItem> Tags,
    IReadOnlyCollection<CustomerCustomFieldItem> CustomFields,
    IReadOnlyCollection<CustomerExternalLinkItem> ExternalLinks,
    IReadOnlyCollection<CustomerDocumentItem> Documents,
    IReadOnlyCollection<CustomerTimelineItem> Timeline);

public sealed record CustomerAddressItem(
    Guid Id,
    string AddressType,
    string? Line1,
    string? Line2,
    string? City,
    string? Region,
    string? PostalCode,
    string Country,
    bool IsPrimary,
    bool IsBilling,
    bool IsShipping);

public sealed record CustomerPhoneItem(
    Guid Id,
    string PhoneType,
    string PhoneNumber,
    string? Extension,
    bool IsPrimary,
    bool CanText);

public sealed record CustomerUnitItem(Guid Id, string Description, string? Vin, decimal? Mileage, decimal? Hours, bool IsActive);

public sealed record CustomerNoteItem(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string NoteType,
    string? Subject,
    string Body,
    bool IsPinned,
    string? AuthorDisplayName);

public sealed record CustomerTagItem(Guid Id, string Tag);

public sealed record CustomerCustomFieldItem(Guid Id, string FieldKey, string? FieldLabel, string? FieldValue);

public sealed record CustomerExternalLinkItem(Guid Id, string Provider, string ExternalCustomerId, string? ExternalUrl, bool IsActive);

public sealed record CustomerDocumentItem(Guid Id, string FileName, string DocumentType, string? Url, string? ContentType, DateTimeOffset UploadedAtUtc);

public sealed record CustomerTimelineItem(DateTimeOffset OccurredAtUtc, string EventType, string Summary);

public sealed record CustomerSearchRequest(string? Query, string? Status);

public sealed record CreateCustomerRequest(
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string CustomerType,
    string Status,
    string? LifecycleStage,
    string? Source,
    string? PreferredContactMethod);

public sealed record UpdateCustomerRequest(
    Guid CustomerId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string CustomerType,
    string Status,
    string? LifecycleStage,
    string? Source,
    string? PreferredContactMethod);

public sealed record AddCustomerNoteRequest(Guid CustomerId, string NoteType, string? Subject, string Body, bool IsPinned);

public sealed record AddCustomerAddressRequest(
    Guid CustomerId,
    string AddressType,
    string? Line1,
    string? Line2,
    string? City,
    string? Region,
    string? PostalCode,
    string Country,
    bool IsPrimary,
    bool IsBilling,
    bool IsShipping);

public sealed record AddCustomerPhoneRequest(Guid CustomerId, string PhoneType, string PhoneNumber, string? Extension, bool IsPrimary, bool CanText);

public sealed record AddCustomerTagRequest(Guid CustomerId, string Tag);

public sealed record AddCustomerCustomFieldRequest(Guid CustomerId, string FieldKey, string? FieldLabel, string? FieldValue);

public sealed record AddCustomerExternalLinkRequest(Guid CustomerId, string Provider, string ExternalCustomerId, string? ExternalUrl);

public sealed record AddCustomerDocumentRequest(Guid CustomerId, string FileName, string DocumentType, string? Url, string? ContentType);
