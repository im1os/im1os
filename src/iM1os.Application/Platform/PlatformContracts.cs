namespace iM1os.Application.Platform;

public sealed record PlatformLoginRequest(string Email, string Password);

public sealed record PlatformLoginResult(Guid UserId, string Email, string DisplayName, string Role);

public sealed record ProvisionTenantRequest(
    string BusinessName,
    string BusinessEmail,
    string OwnerName,
    string OwnerEmail,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string TimeZone,
    string SubscriptionPlan,
    bool IsTrial,
    IReadOnlyCollection<string> DefaultModules,
    string DefaultLanguage,
    string DefaultCurrency);

public sealed record ProvisionTenantResult(Guid OrganizationId, Guid OwnerUserId, Guid LocationId, Guid PlatformTenantId);

public sealed record TenantManagerRow(
    Guid OrganizationId,
    string OrganizationName,
    string Status,
    string SubscriptionPlan,
    DateTimeOffset CreatedAtUtc,
    string CurrentVersion,
    string HealthStatus,
    int ActiveUsers,
    int Locations,
    DateTimeOffset? TrialExpiresAtUtc,
    string BillingStatus,
    string ProvisioningStatus);

public sealed record TenantManagerDetail(
    TenantManagerRow Tenant,
    string? OwnerName,
    string? OwnerEmail,
    bool OwnerActivated,
    IReadOnlyCollection<string> ModulesEnabled,
    IReadOnlyCollection<string> FeatureFlags,
    IReadOnlyCollection<string> ProvisioningHistory,
    IReadOnlyCollection<string> AuditHistory,
    IReadOnlyCollection<string> WelcomeEmails);

public sealed record UpdateTenantManagementRequest(
    Guid OrganizationId,
    string OrganizationName,
    string Status,
    string SubscriptionPlan,
    string CurrentVersion,
    string HealthStatus,
    string BillingStatus,
    string ProvisioningStatus,
    DateTimeOffset? TrialExpiresAtUtc);

public sealed record PlatformDashboardSummary(
    int TotalTenants,
    int TrialTenants,
    int ActiveTenants,
    int SuspendedTenants,
    decimal MonthlyRecurringRevenue,
    IReadOnlyCollection<string> RecentProvisioningActivity,
    IReadOnlyCollection<string> LatestPlatformEvents);
