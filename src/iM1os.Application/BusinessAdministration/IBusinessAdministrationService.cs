namespace iM1os.Application.BusinessAdministration;

public interface IBusinessAdministrationService
{
    Task<BusinessAdministrationWorkspace> GetWorkspaceAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task UpdateBusinessProfileAsync(Guid organizationId, Guid userId, UpdateBusinessProfileRequest request, CancellationToken cancellationToken);

    Task UpsertLocationAsync(Guid organizationId, Guid userId, UpsertLocationRequest request, CancellationToken cancellationToken);

    Task InviteEmployeeAsync(Guid organizationId, Guid userId, InviteEmployeeRequest request, CancellationToken cancellationToken);

    Task SaveLaborConfigurationAsync(Guid organizationId, Guid userId, LaborConfigurationRequest request, CancellationToken cancellationToken);

    Task SaveTaxConfigurationAsync(Guid organizationId, Guid userId, TaxConfigurationRequest request, CancellationToken cancellationToken);

    Task SaveNotificationPreferencesAsync(Guid organizationId, Guid userId, NotificationPreferencesRequest request, CancellationToken cancellationToken);
}
