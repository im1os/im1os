namespace iM1os.Application.FinancialServices.Merchant;

public interface IMerchantAccountService
{
    Task<CompanyMerchantAccountWorkspace> GetCompanyMerchantAccountAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<PlatformMerchantApplicationsWorkspace> GetPlatformMerchantApplicationsAsync(CancellationToken cancellationToken);

    Task<PlatformMerchantApplicationDetail> GetPlatformMerchantApplicationAsync(
        Guid organizationId,
        Guid merchantAccountId,
        CancellationToken cancellationToken);

    Task<PlatformActiveMerchantsWorkspace> GetPlatformActiveMerchantsAsync(CancellationToken cancellationToken);

    Task<MerchantAccountResult> OnboardMerchantAsync(
        Guid organizationId,
        Guid actorUserId,
        MerchantOnboardingRequest request,
        CancellationToken cancellationToken);

    Task<MerchantAccountResult> SaveDraftAsync(
        Guid organizationId,
        Guid actorUserId,
        MerchantApplicationRequest request,
        CancellationToken cancellationToken);

    Task<MerchantAccountResult> SubmitApplicationAsync(
        Guid organizationId,
        Guid actorUserId,
        MerchantApplicationRequest request,
        CancellationToken cancellationToken);

    Task<MerchantAccountResult> ApproveApplicationAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<MerchantAccountResult> RefreshProviderStatusAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<MerchantAccountResult> RejectApplicationAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        string? reason,
        CancellationToken cancellationToken);

    Task<MerchantAccountResult> ChangeStatusAsync(
        Guid organizationId,
        Guid merchantAccountId,
        Guid actorUserId,
        string newStatus,
        string? reason,
        CancellationToken cancellationToken);
}
