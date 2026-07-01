namespace iM1os.Application.CompanySuppliers;

public interface ICompanySupplierService
{
    Task<CompanyWpsConnectorPage> GetWpsConnectorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task<CompanyWpsConnectorPage> SaveWpsConnectorAsync(Guid organizationId, Guid userId, CompanyWpsConnectorSettingsRequest request, CancellationToken cancellationToken);

    Task<CompanyWpsConnectorPage> QueueWpsDealerPricingSyncAsync(Guid organizationId, Guid userId, CompanyWpsDealerPricingSyncRequest request, CancellationToken cancellationToken);

    Task<CompanySupplierConnectorPage> GetPartsUnlimitedConnectorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task<CompanySupplierConnectorPage> SavePartsUnlimitedConnectorAsync(Guid organizationId, Guid userId, CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken);

    Task<CompanySupplierConnectorPage> QueuePartsUnlimitedDealerPricingSyncAsync(Guid organizationId, Guid userId, CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken);

    Task<CompanySupplierConnectorPage> GetTurn14ConnectorAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task<CompanySupplierConnectorPage> SaveTurn14ConnectorAsync(Guid organizationId, Guid userId, CompanySupplierConnectorSettingsRequest request, CancellationToken cancellationToken);

    Task<CompanySupplierConnectorPage> QueueTurn14DealerPricingSyncAsync(Guid organizationId, Guid userId, CompanySupplierDealerPricingSyncRequest request, CancellationToken cancellationToken);
}
