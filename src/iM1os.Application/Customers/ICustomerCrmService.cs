namespace iM1os.Application.Customers;

public interface ICustomerCrmService
{
    Task<CustomerWorkspace> GetWorkspaceAsync(Guid organizationId, CustomerSearchRequest request, CancellationToken cancellationToken);

    Task<CustomerDetail?> GetDetailAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);

    Task<Guid> CreateCustomerAsync(Guid organizationId, Guid actorUserId, CreateCustomerRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task UpdateCustomerAsync(Guid organizationId, Guid actorUserId, UpdateCustomerRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddNoteAsync(Guid organizationId, Guid actorUserId, AddCustomerNoteRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddAddressAsync(Guid organizationId, Guid actorUserId, AddCustomerAddressRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddPhoneAsync(Guid organizationId, Guid actorUserId, AddCustomerPhoneRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddUnitAsync(Guid organizationId, Guid actorUserId, AddCustomerUnitRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddUnitAttachmentAsync(Guid organizationId, Guid actorUserId, AddCustomerUnitAttachmentRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddTagAsync(Guid organizationId, Guid actorUserId, AddCustomerTagRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddCustomFieldAsync(Guid organizationId, Guid actorUserId, AddCustomerCustomFieldRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddExternalLinkAsync(Guid organizationId, Guid actorUserId, AddCustomerExternalLinkRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddDocumentAsync(Guid organizationId, Guid actorUserId, AddCustomerDocumentRequest request, string? ipAddress, CancellationToken cancellationToken);
}
