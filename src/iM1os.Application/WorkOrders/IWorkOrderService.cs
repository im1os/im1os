namespace iM1os.Application.WorkOrders;

public interface IWorkOrderService
{
    Task<WorkOrderWorkspace> GetWorkspaceAsync(Guid organizationId, WorkOrderSearchRequest request, CancellationToken cancellationToken);

    Task<WorkOrderEditor> GetNewEditorAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<WorkOrderEditor?> GetEditorAsync(Guid organizationId, Guid workOrderId, CancellationToken cancellationToken);

    Task<Guid> SaveAsync(Guid organizationId, Guid actorUserId, SaveWorkOrderRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<WorkOrderIntakePage> GetIntakeAsync(Guid organizationId, Guid? verifiedEmployeeId, CancellationToken cancellationToken);

    Task<WorkOrderIntakePinResult?> VerifyIntakePinAsync(Guid organizationId, string? pin, CancellationToken cancellationToken);

    Task<WorkOrderCustomerLookupResult> LookupCustomerAsync(Guid organizationId, string? query, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetYmmTypesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<int>> GetYmmYearsAsync(string? vehicleType, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetYmmMakesAsync(string? vehicleType, int year, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetYmmModelsAsync(string? vehicleType, int year, string make, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkOrderLaborItem>> SearchLaborItemsAsync(Guid organizationId, string? query, int limit, CancellationToken cancellationToken);

    Task<WorkOrderIntakeResult> CreateFromIntakeAsync(Guid organizationId, Guid actorUserId, CreateWorkOrderIntakeRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddAttachmentsAsync(Guid organizationId, Guid actorUserId, AddWorkOrderAttachmentRequest request, string? ipAddress, CancellationToken cancellationToken);
}
