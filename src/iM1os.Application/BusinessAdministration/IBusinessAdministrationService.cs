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

    Task SaveSupplierPreferencesAsync(Guid organizationId, Guid userId, SupplierPreferencesRequest request, CancellationToken cancellationToken);

    Task ClockEmployeeAsync(Guid organizationId, Guid userId, ClockEmployeeRequest request, CancellationToken cancellationToken);

    Task AddTimePunchAsync(Guid organizationId, Guid userId, AddTimePunchRequest request, CancellationToken cancellationToken);

    Task UpdateTimePunchAsync(Guid organizationId, Guid userId, UpdateTimePunchRequest request, CancellationToken cancellationToken);

    Task DeleteTimePunchAsync(Guid organizationId, Guid userId, DeleteTimePunchRequest request, CancellationToken cancellationToken);

    Task AddScheduleShiftAsync(Guid organizationId, Guid userId, AddScheduleShiftRequest request, CancellationToken cancellationToken);

    Task DeleteScheduleShiftAsync(Guid organizationId, Guid userId, DeleteScheduleShiftRequest request, CancellationToken cancellationToken);

    Task AddTimeOffAsync(Guid organizationId, Guid userId, AddTimeOffRequest request, CancellationToken cancellationToken);

    Task SetTimeOffStatusAsync(Guid organizationId, Guid userId, SetTimeOffStatusRequest request, CancellationToken cancellationToken);

    Task DeleteTimeOffAsync(Guid organizationId, Guid userId, DeleteTimeOffRequest request, CancellationToken cancellationToken);

    Task AddCompanyAssetAsync(Guid organizationId, Guid userId, AddCompanyAssetRequest request, CancellationToken cancellationToken);

    Task ReturnCompanyAssetAsync(Guid organizationId, Guid userId, ReturnCompanyAssetRequest request, CancellationToken cancellationToken);

    Task DeleteCompanyAssetAsync(Guid organizationId, Guid userId, DeleteCompanyAssetRequest request, CancellationToken cancellationToken);

    Task AddSafetyIncidentAsync(Guid organizationId, Guid userId, AddSafetyIncidentRequest request, CancellationToken cancellationToken);
}
