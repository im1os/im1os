namespace iM1os.Application.Inventory;

public interface ICompanyInventoryService
{
    Task<CompanyInventoryWorkspace> GetWorkspaceAsync(Guid organizationId, CompanyInventorySearchRequest request, CancellationToken cancellationToken);

    Task<CompanyInventoryAddPage> GetAddPageAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<CompanyInventorySupplierLookupResult> LookupSupplierItemAsync(Guid organizationId, CompanyInventorySupplierLookupRequest request, CancellationToken cancellationToken);

    Task<Guid> CreateCustomItemAsync(Guid organizationId, Guid actorUserId, CompanyInventoryItemRequest request, CancellationToken cancellationToken);

    Task<Guid> AddSupplierItemAsync(Guid organizationId, Guid actorUserId, CompanyInventorySupplierItemRequest request, CancellationToken cancellationToken);

    Task SaveLocationStockAsync(Guid organizationId, Guid actorUserId, CompanyInventoryLocationStockRequest request, CancellationToken cancellationToken);

    Task AdjustStockAsync(Guid organizationId, Guid actorUserId, CompanyInventoryStockAdjustmentRequest request, CancellationToken cancellationToken);

    Task<CompanyInventoryImportResult> ImportCsvAsync(Guid organizationId, Guid actorUserId, Stream csvStream, CancellationToken cancellationToken);
}
