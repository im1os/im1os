namespace iM1os.Application.TenantIdentity;

public interface IBusinessOnboardingService
{
    Task<BusinessOnboardingRequest?> GetDraftAsync(Guid organizationId, CancellationToken cancellationToken);

    Task CompleteAsync(Guid organizationId, Guid userId, BusinessOnboardingRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<BusinessDashboardSummary> GetDashboardAsync(Guid organizationId, CancellationToken cancellationToken);
}
