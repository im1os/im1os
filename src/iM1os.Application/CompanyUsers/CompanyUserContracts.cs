namespace iM1os.Application.CompanyUsers;

public sealed record CompanyUsersWorkspace(
    IReadOnlyCollection<CompanyUserRow> Users,
    IReadOnlyCollection<CompanyRoleOption> Roles,
    IReadOnlyCollection<PermissionDefinitionDto> Permissions,
    CompanyUserEditor? SelectedUser,
    string? Query,
    string? Status,
    string? Role);

public sealed record CompanyUserRow(
    Guid Id,
    string Avatar,
    string Name,
    string Email,
    string CompanyRole,
    string Status,
    DateTimeOffset? LastLoginAtUtc,
    bool MfaEnabled,
    DateTimeOffset CreatedAtUtc);

public sealed record CompanyUserEditor(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string Email,
    string? Phone,
    string? JobTitle,
    string CompanyRole,
    string Status,
    CompanyUserSecurity Security,
    IReadOnlyCollection<CompanyUserPermissionState> Permissions,
    IReadOnlyCollection<CompanyUserActivity> Activity);

public sealed record CompanyRoleOption(Guid Id, string Name, bool IsSystemRole);

public sealed record PermissionDefinitionDto(Guid Id, string Key, string Description);

public sealed record CompanyUserPermissionState(
    Guid PermissionId,
    string Key,
    string Description,
    bool RoleAllows,
    bool? OverrideAllows);

public sealed record CompanyUserSecurity(
    string Username,
    string Email,
    DateTimeOffset? LastPasswordChangedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    int FailedLoginAttempts,
    bool MfaEnabled,
    bool AccountLocked,
    int ActiveSessions,
    bool MustChangePassword);

public sealed record CompanyUserActivity(DateTimeOffset OccurredAtUtc, string EventType, string Summary);

public sealed record CompanyUserSearchRequest(string? Query, string? Status, string? Role, Guid? SelectedUserId);

public sealed record CreateCompanyUserRequest(
    string Name,
    string Email,
    string RoleName,
    string Status,
    bool SendInvitationEmail,
    string? InitialPassword);

public sealed record UpdateCompanyUserRequest(
    Guid UserId,
    string FirstName,
    string LastName,
    string DisplayName,
    string Email,
    string? Phone,
    string? JobTitle,
    string RoleName,
    string Status);

public sealed record SaveCompanyUserPermissionOverridesRequest(
    Guid UserId,
    IReadOnlyCollection<CompanyUserPermissionOverrideRequest> Overrides);

public sealed record CompanyUserPermissionOverrideRequest(Guid PermissionId, bool IsOverridden, bool IsAllowed);

public sealed record CompanyUserSecurityActionRequest(Guid UserId, string Action, string? NewPassword = null);
