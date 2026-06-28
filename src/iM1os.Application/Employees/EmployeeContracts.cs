namespace iM1os.Application.Employees;

public sealed record EmployeesWorkspace(
    IReadOnlyCollection<EmployeeRow> Employees,
    IReadOnlyCollection<EmployeeRoleOption> Roles,
    IReadOnlyCollection<EmployeePermissionDefinition> Permissions,
    EmployeeEditor? SelectedEmployee,
    string? Query,
    string? Status,
    string? Role);

public sealed record EmployeeRow(
    Guid Id,
    string Avatar,
    string Name,
    string? Email,
    string? JobTitle,
    string Status,
    bool HasLoginAccount,
    string LoginStatus,
    string CompanyRole,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record EmployeeEditor(
    Guid Id,
    string? EmployeeNumber,
    string FirstName,
    string LastName,
    string DisplayName,
    string? Email,
    string? Phone,
    string? JobTitle,
    string? Department,
    string EmploymentType,
    string Status,
    DateOnly? HireDate,
    DateOnly? TerminationDate,
    EmployeeLoginAccount? LoginAccount,
    IReadOnlyCollection<EmployeeCompensationItem> Compensation,
    IReadOnlyCollection<EmployeePermissionState> Permissions,
    IReadOnlyCollection<EmployeeActivity> Activity);

public sealed record EmployeeLoginAccount(
    Guid UserId,
    string Username,
    string Email,
    string CompanyRole,
    string Status,
    bool HasPin,
    EmployeeSecurity Security);

public sealed record EmployeeCompensationItem(
    Guid Id,
    string PayrollType,
    decimal? HourlyRate,
    decimal? SalaryAmount,
    decimal? WorkOrderCommissionRate,
    decimal? SalesCommissionRate,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    string? Notes);

public sealed record EmployeeRoleOption(Guid Id, string Name, bool IsSystemRole);

public sealed record EmployeePermissionDefinition(Guid Id, string Key, string Description);

public sealed record EmployeePermissionState(
    Guid PermissionId,
    string Key,
    string Description,
    bool RoleAllows,
    bool? OverrideAllows);

public sealed record EmployeeSecurity(
    DateTimeOffset? LastPasswordChangedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    int FailedLoginAttempts,
    bool MfaEnabled,
    bool AccountLocked,
    int ActiveSessions,
    bool MustChangePassword);

public sealed record EmployeeActivity(DateTimeOffset OccurredAtUtc, string EventType, string Summary);

public sealed record EmployeeSearchRequest(string? Query, string? Status, string? Role, Guid? SelectedEmployeeId);

public sealed record CreateEmployeeRequest(
    string Name,
    string? Email,
    string? Phone,
    string? JobTitle,
    string? Department,
    string EmploymentType,
    string Status,
    bool EnableLoginAccount,
    string? RoleName,
    bool SendInvitationEmail,
    string? InitialPassword);

public sealed record UpdateEmployeeRequest(
    Guid EmployeeId,
    string? EmployeeNumber,
    string FirstName,
    string LastName,
    string DisplayName,
    string? Email,
    string? Phone,
    string? JobTitle,
    string? Department,
    string EmploymentType,
    string Status,
    DateOnly? HireDate,
    DateOnly? TerminationDate,
    string? RoleName);

public sealed record EnableEmployeeLoginRequest(
    Guid EmployeeId,
    string Email,
    string RoleName,
    bool SendInvitationEmail,
    string? InitialPassword);

public sealed record SaveEmployeeCompensationRequest(
    Guid EmployeeId,
    string PayrollType,
    decimal? HourlyRate,
    decimal? SalaryAmount,
    decimal? WorkOrderCommissionRate,
    decimal? SalesCommissionRate,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    string? Notes);

public sealed record SaveEmployeePinRequest(Guid EmployeeId, string Pin);

public sealed record SaveEmployeePermissionOverridesRequest(
    Guid EmployeeId,
    IReadOnlyCollection<EmployeePermissionOverrideRequest> Overrides);

public sealed record EmployeePermissionOverrideRequest(Guid PermissionId, bool IsOverridden, bool IsAllowed);

public sealed record EmployeeSecurityActionRequest(Guid EmployeeId, string Action, string? NewPassword = null);
