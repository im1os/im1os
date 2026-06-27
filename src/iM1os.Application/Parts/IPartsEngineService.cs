namespace iM1os.Application.Parts;

public interface IPartsEngineService
{
    Task<PartDetail> CreateManufacturerPartAsync(CreateManufacturerPartRequest request, CancellationToken cancellationToken);

    Task<PartDetail?> GetPartDetailAsync(Guid manufacturerPartId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PartSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    Task<SupplierListingDetail> AddSupplierListingAsync(AddSupplierListingRequest request, CancellationToken cancellationToken);

    Task<InventoryItemDetail> SetInventoryItemAsync(SetInventoryItemRequest request, CancellationToken cancellationToken);

    Task<PartDetail> SupersedePartAsync(Guid manufacturerPartId, SupersedePartRequest request, CancellationToken cancellationToken);
}
