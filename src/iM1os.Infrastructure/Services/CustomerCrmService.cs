using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Customers;
using iM1os.Domain.Audit;
using iM1os.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class CustomerCrmService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : ICustomerCrmService
{
    public async Task<CustomerWorkspace> GetWorkspaceAsync(Guid organizationId, CustomerSearchRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var search = request.Query.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.DisplayName.ToUpper().Contains(search) ||
                (x.CompanyName != null && x.CompanyName.ToUpper().Contains(search)) ||
                (x.Email != null && x.Email.ToUpper().Contains(search)) ||
                (x.Phone != null && x.Phone.ToUpper().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.Status == request.Status);
        }

        var customers = await query
            .OrderBy(x => x.DisplayName)
            .Select(x => new CustomerRow(
                x.Id,
                x.DisplayName,
                x.CompanyName,
                x.Email,
                x.Phone,
                x.Status,
                x.LifecycleStage,
                x.Source,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new CustomerWorkspace(customers, request.Query, request.Status);
    }

    public async Task<CustomerDetail?> GetDetailAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == customerId && x.DeletedAtUtc == null)
            .Include(x => x.Addresses)
            .Include(x => x.PhoneNumbers)
            .Include(x => x.Notes)
            .Include(x => x.Tags)
            .Include(x => x.CustomFields)
            .Include(x => x.ExternalLinks)
            .Include(x => x.Documents)
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var unitEntities = await dbContext.CustomerVehicles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var units = unitEntities
            .Select(x => new CustomerUnitItem(
                x.Id,
                string.Join(" ", new[] { x.Year?.ToString(), x.Make, x.Model, x.Trim }.Where(v => !string.IsNullOrWhiteSpace(v))),
                x.Vin,
                x.Mileage,
                x.Hours,
                x.IsActive))
            .ToList();

        var timeline = await dbContext.TimelineEvents.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.EntityType == "Customer" && x.EntityId == customerId.ToString())
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(50)
            .Select(x => new CustomerTimelineItem(x.OccurredAtUtc, x.EventType, x.Summary))
            .ToListAsync(cancellationToken);

        return new CustomerDetail(
            customer.Id,
            customer.DisplayName,
            customer.FirstName,
            customer.LastName,
            customer.CompanyName,
            customer.Email,
            customer.Phone,
            customer.CustomerType,
            customer.Status,
            customer.LifecycleStage,
            customer.Source,
            customer.PreferredContactMethod,
            customer.Addresses.Where(x => x.DeletedAtUtc == null).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.AddressType).Select(x => new CustomerAddressItem(x.Id, x.AddressType, x.Line1, x.Line2, x.City, x.Region, x.PostalCode, x.Country, x.IsPrimary, x.IsBilling, x.IsShipping)).ToList(),
            customer.PhoneNumbers.Where(x => x.DeletedAtUtc == null).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.PhoneType).Select(x => new CustomerPhoneItem(x.Id, x.PhoneType, x.PhoneNumber, x.Extension, x.IsPrimary, x.CanText)).ToList(),
            units,
            customer.Notes.Where(x => x.DeletedAtUtc == null).OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.OccurredAtUtc).Select(x => new CustomerNoteItem(x.Id, x.OccurredAtUtc, x.NoteType, x.Subject, x.Body, x.IsPinned, x.AuthorDisplayName)).ToList(),
            customer.Tags.OrderBy(x => x.Tag).Select(x => new CustomerTagItem(x.Id, x.Tag)).ToList(),
            customer.CustomFields.OrderBy(x => x.FieldKey).Select(x => new CustomerCustomFieldItem(x.Id, x.FieldKey, x.FieldLabel, x.FieldValue)).ToList(),
            customer.ExternalLinks.OrderBy(x => x.Provider).Select(x => new CustomerExternalLinkItem(x.Id, x.Provider, x.ExternalCustomerId, x.ExternalUrl, x.IsActive)).ToList(),
            customer.Documents.Where(x => x.DeletedAtUtc == null).OrderByDescending(x => x.UploadedAtUtc).Select(x => new CustomerDocumentItem(x.Id, x.FileName, x.DocumentType, x.Url, x.ContentType, x.UploadedAtUtc)).ToList(),
            timeline);
    }

    public async Task<Guid> CreateCustomerAsync(Guid organizationId, Guid actorUserId, CreateCustomerRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            OrganizationId = organizationId,
            DisplayName = Required(request.DisplayName, "Display name"),
            FirstName = Clean(request.FirstName),
            LastName = Clean(request.LastName),
            CompanyName = Clean(request.CompanyName),
            Email = Clean(request.Email),
            Phone = Clean(request.Phone),
            CustomerType = Required(request.CustomerType, "Customer type"),
            Status = Required(request.Status, "Status"),
            LifecycleStage = Clean(request.LifecycleStage),
            Source = Clean(request.Source),
            PreferredContactMethod = Clean(request.PreferredContactMethod),
            IsActive = request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
        };

        dbContext.Customers.Add(customer);
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerCreated", "Customer created", ipAddress, new { customer.DisplayName, customer.Email, customer.Phone });
        await dbContext.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    public async Task UpdateCustomerAsync(Guid organizationId, Guid actorUserId, UpdateCustomerRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        var before = Snapshot(customer);
        customer.DisplayName = Required(request.DisplayName, "Display name");
        customer.FirstName = Clean(request.FirstName);
        customer.LastName = Clean(request.LastName);
        customer.CompanyName = Clean(request.CompanyName);
        customer.Email = Clean(request.Email);
        customer.Phone = Clean(request.Phone);
        customer.CustomerType = Required(request.CustomerType, "Customer type");
        customer.Status = Required(request.Status, "Status");
        customer.LifecycleStage = Clean(request.LifecycleStage);
        customer.Source = Clean(request.Source);
        customer.PreferredContactMethod = Clean(request.PreferredContactMethod);
        customer.IsActive = customer.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

        AddActivity(organizationId, customer.Id, actorUserId, "CustomerUpdated", "Customer updated", ipAddress, new { before, after = Snapshot(customer) });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddNoteAsync(Guid organizationId, Guid actorUserId, AddCustomerNoteRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        var author = await ActorNameAsync(organizationId, actorUserId, cancellationToken);
        var note = new CustomerNote
        {
            OrganizationId = organizationId,
            CustomerId = customer.Id,
            EmployeeId = await ActorEmployeeIdAsync(organizationId, actorUserId, cancellationToken),
            AuthorDisplayName = author,
            NoteType = Required(request.NoteType, "Note type"),
            Subject = Clean(request.Subject),
            Body = Required(request.Body, "Note"),
            IsPinned = request.IsPinned,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };
        dbContext.CustomerNotes.Add(note);
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerNoteAdded", "Customer note added", ipAddress, new { note.NoteType, note.Subject, note.IsPinned });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAddressAsync(Guid organizationId, Guid actorUserId, AddCustomerAddressRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        dbContext.CustomerAddresses.Add(new CustomerAddress
        {
            OrganizationId = organizationId,
            CustomerId = customer.Id,
            AddressType = Required(request.AddressType, "Address type"),
            Line1 = Clean(request.Line1),
            Line2 = Clean(request.Line2),
            City = Clean(request.City),
            Region = Clean(request.Region),
            PostalCode = Clean(request.PostalCode),
            Country = Required(request.Country, "Country"),
            IsPrimary = request.IsPrimary,
            IsBilling = request.IsBilling,
            IsShipping = request.IsShipping
        });
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerAddressAdded", "Customer address added", ipAddress, new { request.AddressType, request.City, request.Region });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPhoneAsync(Guid organizationId, Guid actorUserId, AddCustomerPhoneRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        dbContext.CustomerPhoneNumbers.Add(new CustomerPhoneNumber
        {
            OrganizationId = organizationId,
            CustomerId = customer.Id,
            PhoneType = Required(request.PhoneType, "Phone type"),
            PhoneNumber = Required(request.PhoneNumber, "Phone number"),
            Extension = Clean(request.Extension),
            IsPrimary = request.IsPrimary,
            CanText = request.CanText
        });
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerPhoneAdded", "Customer phone added", ipAddress, new { request.PhoneType, request.PhoneNumber });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTagAsync(Guid organizationId, Guid actorUserId, AddCustomerTagRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        var tag = Required(request.Tag, "Tag");
        if (!await dbContext.CustomerTags.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.CustomerId == customer.Id && x.Tag == tag, cancellationToken))
        {
            dbContext.CustomerTags.Add(new CustomerTag { OrganizationId = organizationId, CustomerId = customer.Id, Tag = tag });
            AddActivity(organizationId, customer.Id, actorUserId, "CustomerTagAdded", "Customer tag added", ipAddress, new { Tag = tag });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddCustomFieldAsync(Guid organizationId, Guid actorUserId, AddCustomerCustomFieldRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        var fieldKey = Required(request.FieldKey, "Field key");
        var existing = await dbContext.CustomerCustomFields.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.CustomerId == customer.Id && x.FieldKey == fieldKey, cancellationToken);
        if (existing is null)
        {
            dbContext.CustomerCustomFields.Add(new CustomerCustomField { OrganizationId = organizationId, CustomerId = customer.Id, FieldKey = fieldKey, FieldLabel = Clean(request.FieldLabel), FieldValue = Clean(request.FieldValue) });
        }
        else
        {
            existing.FieldLabel = Clean(request.FieldLabel);
            existing.FieldValue = Clean(request.FieldValue);
        }
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerCustomFieldSaved", "Customer custom field saved", ipAddress, new { FieldKey = fieldKey });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddExternalLinkAsync(Guid organizationId, Guid actorUserId, AddCustomerExternalLinkRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        var link = new CustomerExternalLink
        {
            OrganizationId = organizationId,
            CustomerId = customer.Id,
            Provider = Required(request.Provider, "Provider"),
            ExternalCustomerId = Required(request.ExternalCustomerId, "External customer id"),
            ExternalUrl = Clean(request.ExternalUrl)
        };
        dbContext.CustomerExternalLinks.Add(link);
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerExternalLinkAdded", "Customer external link added", ipAddress, new { link.Provider, link.ExternalCustomerId });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddDocumentAsync(Guid organizationId, Guid actorUserId, AddCustomerDocumentRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(organizationId, request.CustomerId, cancellationToken);
        var document = new CustomerDocument
        {
            OrganizationId = organizationId,
            CustomerId = customer.Id,
            FileName = Required(request.FileName, "File name"),
            DocumentType = Required(request.DocumentType, "Document type"),
            Url = Clean(request.Url),
            ContentType = Clean(request.ContentType),
            UploadedAtUtc = dateTimeProvider.UtcNow
        };
        dbContext.CustomerDocuments.Add(document);
        AddActivity(organizationId, customer.Id, actorUserId, "CustomerDocumentAdded", "Customer document added", ipAddress, new { document.FileName, document.DocumentType });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Customer> LoadCustomerAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken)
    {
        return await dbContext.Customers.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == customerId && x.DeletedAtUtc == null, cancellationToken);
    }

    private async Task<Guid?> ActorEmployeeIdAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == actorUserId)
            .Select(x => x.EmployeeId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> ActorNameAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == actorUserId)
            .Select(x => x.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private void AddActivity(Guid organizationId, Guid customerId, Guid actorUserId, string eventType, string summary, string? ipAddress, object payload)
    {
        var now = dateTimeProvider.UtcNow;
        var payloadJson = JsonSerializer.Serialize(payload);
        dbContext.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = actorUserId.ToString(),
            Action = eventType,
            EntityName = "Customer",
            EntityId = customerId.ToString(),
            ChangesJson = payloadJson,
            OccurredAtUtc = now
        });
        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            EntityType = "Customer",
            EntityId = customerId.ToString(),
            EventType = eventType,
            ActorUserId = actorUserId.ToString(),
            Summary = summary,
            OccurredAtUtc = now,
            PayloadJson = payloadJson
        });
    }

    private static object Snapshot(Customer customer) => new
    {
        customer.DisplayName,
        customer.FirstName,
        customer.LastName,
        customer.CompanyName,
        customer.Email,
        customer.Phone,
        customer.CustomerType,
        customer.Status,
        customer.LifecycleStage,
        customer.Source,
        customer.PreferredContactMethod
    };

    private static string Required(string value, string fieldName) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{fieldName} is required.") : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
