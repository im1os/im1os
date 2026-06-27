namespace iM1os.Application.TenantIdentity;

public sealed record TenantLoginRequest(string Email, string Password, Guid? OrganizationId, bool RememberMe);

public sealed record TenantLoginResult(
    Guid UserId,
    Guid OrganizationId,
    string Email,
    string DisplayName,
    string OrganizationName,
    string? OrganizationLogoUrl,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    bool RequiresOnboarding);

public sealed record ActivateOwnerRequest(string Token, string Password, string ConfirmPassword);

public sealed record PasswordResetRequestDto(string Email, Guid? OrganizationId);

public sealed record CompletePasswordResetRequest(string Token, string Password, string ConfirmPassword);

public sealed record BusinessOnboardingRequest(
    string BusinessName,
    string BusinessEmail,
    string Phone,
    string TimeZone,
    string MondayHours,
    string TuesdayHours,
    string WednesdayHours,
    string ThursdayHours,
    string FridayHours,
    string SaturdayHours,
    string SundayHours,
    decimal LaborRate,
    bool InviteEmployeesLater,
    bool ConnectSuppliersLater,
    bool ConnectMerchantServicesLater);

public sealed record BusinessDashboardSummary(
    string BusinessName,
    string Subscription,
    string OrganizationStatus,
    int Locations,
    int Employees,
    int SetupProgress,
    IReadOnlyCollection<string> RecentActivity);

public sealed record TenantProfile(
    string Name,
    string Email,
    string? Phone,
    string Language,
    string TimeZone);

public sealed record UpdateTenantProfileRequest(
    string Name,
    string? Phone,
    string Language,
    string TimeZone);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
