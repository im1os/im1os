using System.Security.Cryptography;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Employees;
using iM1os.Application.Platform;
using iM1os.Domain.Audit;
using iM1os.Domain.Employees;
using iM1os.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class EmployeeService(
    IApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IWelcomeEmailSender welcomeEmailSender,
    IDateTimeProvider dateTimeProvider) : IEmployeeService
{
    private static readonly string[] DefaultRoleNames =
    [
        "Owner",
        "Administrator",
        "Manager",
        "Sales",
        "Technician",
        "Accounting",
        "Inventory",
        "Read Only"
    ];

    public async Task<EmployeesWorkspace> GetWorkspaceAsync(Guid organizationId, Guid actorUserId, EmployeeSearchRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageEmployeesAsync(organizationId, actorUserId, cancellationToken);
        await EnsureDefaultRolesAsync(organizationId, cancellationToken);

        var roles = await dbContext.Roles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new EmployeeRoleOption(x.Id, x.Name, x.IsSystemRole))
            .ToListAsync(cancellationToken);
        var permissions = await dbContext.Permissions
            .OrderBy(x => x.Key)
            .Select(x => new EmployeePermissionDefinition(x.Id, x.Key, x.Description))
            .ToListAsync(cancellationToken);

        var query = dbContext.Employees.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var search = request.Query.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.DisplayName.ToUpper().Contains(search) ||
                (x.Email != null && x.Email.ToUpper().Contains(search)) ||
                (x.Phone != null && x.Phone.ToUpper().Contains(search)) ||
                (x.JobTitle != null && x.JobTitle.ToUpper().Contains(search)) ||
                (x.Department != null && x.Department.ToUpper().Contains(search)) ||
                (x.EmployeeNumber != null && x.EmployeeNumber.ToUpper().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(x => x.LoginAccount != null && x.LoginAccount.UserRoles.Any(role => role.Role != null && role.Role.Name == request.Role));
        }

        var employees = await query.OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);
        var rows = employees.Select(ToRow).ToList();
        var selectedEmployee = request.SelectedEmployeeId is Guid employeeId
            ? await GetEditorAsync(organizationId, employeeId, permissions, cancellationToken)
            : null;

        return new EmployeesWorkspace(rows, roles, permissions, selectedEmployee, request.Query, request.Status, request.Role);
    }

    public async Task<Guid> CreateEmployeeAsync(Guid organizationId, Guid actorUserId, CreateEmployeeRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageEmployeesAsync(organizationId, actorUserId, cancellationToken);
        ValidateEmployeeRequest(request);
        var now = dateTimeProvider.UtcNow;
        var employee = new Employee
        {
            OrganizationId = organizationId,
            FirstName = FirstName(request.Name),
            LastName = LastName(request.Name),
            DisplayName = Required(request.Name, "Name"),
            Email = Clean(request.Email),
            Phone = Clean(request.Phone),
            JobTitle = Clean(request.JobTitle),
            Department = Clean(request.Department),
            EmploymentType = Required(request.EmploymentType, "Employment type"),
            Status = Required(request.Status, "Status")
        };
        dbContext.Employees.Add(employee);

        if (request.EnableLoginAccount)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new InvalidOperationException("Email is required to enable a login account.");
            }

            await CreateLoginAccountAsync(organizationId, employee, Required(request.Email, "Email"), Required(request.RoleName ?? string.Empty, "Company role"), request.SendInvitationEmail, request.InitialPassword, now, cancellationToken);
        }

        AddActivity(organizationId, employee.Id, actorUserId, "EmployeeCreated", "Employee created", ipAddress, new { employee.DisplayName, employee.Email, employee.Status, LoginEnabled = request.EnableLoginAccount });
        await dbContext.SaveChangesAsync(cancellationToken);
        return employee.Id;
    }

    public async Task UpdateEmployeeAsync(Guid organizationId, Guid actorUserId, UpdateEmployeeRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageEmployeesAsync(organizationId, actorUserId, cancellationToken);
        var employee = await LoadEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        var before = Snapshot(employee);

        employee.EmployeeNumber = Clean(request.EmployeeNumber);
        employee.FirstName = Clean(request.FirstName);
        employee.LastName = Clean(request.LastName);
        employee.DisplayName = Required(request.DisplayName, "Display name");
        employee.Email = Clean(request.Email);
        employee.Phone = Clean(request.Phone);
        employee.JobTitle = Clean(request.JobTitle);
        employee.Department = Clean(request.Department);
        employee.EmploymentType = Required(request.EmploymentType, "Employment type");
        employee.Status = Required(request.Status, "Status");
        employee.HireDate = request.HireDate;
        employee.TerminationDate = request.TerminationDate;

        if (employee.LoginAccount is not null)
        {
            SyncLoginFromEmployee(employee.LoginAccount, employee);
            if (!string.IsNullOrWhiteSpace(request.RoleName))
            {
                var role = await GetRoleAsync(organizationId, request.RoleName, cancellationToken);
                employee.LoginAccount.UserRoles.Clear();
                employee.LoginAccount.UserRoles.Add(new UserRole { UserId = employee.LoginAccount.Id, RoleId = role.Id });
            }
        }

        AddActivity(organizationId, employee.Id, actorUserId, "EmployeeUpdated", "Employee updated", ipAddress, new { before, after = Snapshot(employee) });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnableLoginAccountAsync(Guid organizationId, Guid actorUserId, EnableEmployeeLoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageEmployeesAsync(organizationId, actorUserId, cancellationToken);
        var employee = await LoadEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        if (employee.LoginAccount is not null)
        {
            throw new InvalidOperationException("This employee already has a login account.");
        }

        await CreateLoginAccountAsync(organizationId, employee, request.Email, request.RoleName, request.SendInvitationEmail, request.InitialPassword, dateTimeProvider.UtcNow, cancellationToken);
        AddActivity(organizationId, employee.Id, actorUserId, "LoginAccountEnabled", "Login account enabled", ipAddress, new { request.Email, request.RoleName });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SavePermissionOverridesAsync(Guid organizationId, Guid actorUserId, SaveEmployeePermissionOverridesRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageEmployeesAsync(organizationId, actorUserId, cancellationToken);
        var employee = await LoadEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        var user = employee.LoginAccount ?? throw new InvalidOperationException("This employee does not have a login account.");
        var existing = await dbContext.UserPermissionOverrides.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var byPermission = existing.ToDictionary(x => x.PermissionId);

        foreach (var item in request.Overrides)
        {
            if (!item.IsOverridden)
            {
                if (byPermission.TryGetValue(item.PermissionId, out var remove))
                {
                    dbContext.UserPermissionOverrides.Remove(remove);
                }
                continue;
            }

            if (!byPermission.TryGetValue(item.PermissionId, out var permissionOverride))
            {
                permissionOverride = new UserPermissionOverride
                {
                    OrganizationId = organizationId,
                    UserId = user.Id,
                    PermissionId = item.PermissionId
                };
                dbContext.UserPermissionOverrides.Add(permissionOverride);
            }

            permissionOverride.IsAllowed = item.IsAllowed;
        }

        AddActivity(organizationId, employee.Id, actorUserId, "PermissionsChanged", "Login permissions changed", ipAddress, request.Overrides);
        AddIdentityEvent(organizationId, user.Id, "PermissionsChanged", ipAddress, request.Overrides);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RunSecurityActionAsync(Guid organizationId, Guid actorUserId, EmployeeSecurityActionRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageEmployeesAsync(organizationId, actorUserId, cancellationToken);
        var employee = await LoadEmployeeAsync(organizationId, request.EmployeeId, cancellationToken);
        var user = employee.LoginAccount ?? throw new InvalidOperationException("This employee does not have a login account.");
        var now = dateTimeProvider.UtcNow;
        var action = request.Action.Trim();

        switch (action)
        {
            case "ResetPassword":
                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
                {
                    throw new InvalidOperationException("Password must be at least 12 characters.");
                }

                user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
                user.MustChangePassword = false;
                user.LastPasswordChangedAtUtc = now;
                user.AccessFailedCount = 0;
                user.LockoutEndAtUtc = null;
                AddActivity(organizationId, employee.Id, actorUserId, "PasswordChanged", "Login password changed", ipAddress, new { });
                AddIdentityEvent(organizationId, user.Id, "PasswordChanged", ipAddress, new { });
                break;
            case "SendPasswordResetEmail":
                var token = NewToken();
                dbContext.PasswordResetRequests.Add(new PasswordResetRequest
                {
                    OrganizationId = organizationId,
                    UserId = user.Id,
                    TokenHash = TenantIdentityService.HashToken(token),
                    ExpiresAtUtc = now.AddHours(2)
                });
                await welcomeEmailSender.SendAsync(user.Email, "Reset your iM1 OS password", $"Reset your company password: /company/reset-password?token={Uri.EscapeDataString(token)}", cancellationToken);
                AddActivity(organizationId, employee.Id, actorUserId, "PasswordReset", "Password reset email sent", ipAddress, new { });
                AddIdentityEvent(organizationId, user.Id, "PasswordReset", ipAddress, new { });
                break;
            case "UnlockAccount":
                user.AccessFailedCount = 0;
                user.LockoutEndAtUtc = null;
                AddActivity(organizationId, employee.Id, actorUserId, "AccountUnlocked", "Login account unlocked", ipAddress, new { });
                AddIdentityEvent(organizationId, user.Id, "AccountUnlocked", ipAddress, new { });
                break;
            case "ForcePasswordChange":
                user.MustChangePassword = true;
                AddActivity(organizationId, employee.Id, actorUserId, "ForcePasswordChange", "Password change required", ipAddress, new { });
                AddIdentityEvent(organizationId, user.Id, "ForcePasswordChange", ipAddress, new { });
                break;
            case "SignOutAllSessions":
                var sessions = await dbContext.UserSessions.IgnoreQueryFilters()
                    .Where(x => x.OrganizationId == organizationId && x.UserId == user.Id && x.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);
                foreach (var session in sessions)
                {
                    session.RevokedAtUtc = now;
                }
                AddActivity(organizationId, employee.Id, actorUserId, "SessionsRevoked", "All login sessions signed out", ipAddress, new { Count = sessions.Count });
                AddIdentityEvent(organizationId, user.Id, "SessionsRevoked", ipAddress, new { Count = sessions.Count });
                break;
            case "DisableLogin":
                user.IsActive = false;
                user.DisabledAtUtc ??= now;
                AddActivity(organizationId, employee.Id, actorUserId, "LoginDisabled", "Login account disabled", ipAddress, new { });
                AddIdentityEvent(organizationId, user.Id, "LoginDisabled", ipAddress, new { });
                break;
            case "EnableLogin":
                user.IsActive = true;
                user.DisabledAtUtc = null;
                AddActivity(organizationId, employee.Id, actorUserId, "LoginEnabled", "Login account enabled", ipAddress, new { });
                AddIdentityEvent(organizationId, user.Id, "LoginEnabled", ipAddress, new { });
                break;
            default:
                throw new InvalidOperationException("Unsupported security action.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<EmployeeEditor?> GetEditorAsync(Guid organizationId, Guid employeeId, IReadOnlyCollection<EmployeePermissionDefinition> permissions, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == employeeId && x.DeletedAtUtc == null)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.PermissionOverrides).ThenInclude(x => x.Permission)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.Sessions)
            .SingleOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var user = employee.LoginAccount;
        var role = user?.UserRoles.Select(x => x.Role).FirstOrDefault(x => x is not null);
        var rolePermissionIds = role?.RolePermissions.Select(x => x.PermissionId).ToHashSet() ?? [];
        var overrideByPermission = user?.PermissionOverrides.ToDictionary(x => x.PermissionId, x => x.IsAllowed) ?? [];
        var permissionStates = user is null
            ? []
            : permissions
                .Select(x => new EmployeePermissionState(
                    x.Id,
                    x.Key,
                    x.Description,
                    rolePermissionIds.Contains(x.Id),
                    overrideByPermission.TryGetValue(x.Id, out var allowed) ? allowed : null as bool?))
                .ToList();
        var activity = await GetActivityAsync(organizationId, employee.Id, user?.Id, cancellationToken);
        var activeSessions = user?.Sessions.Count(x => x.RevokedAtUtc == null) ?? 0;
        var loginAccount = user is null
            ? null
            : new EmployeeLoginAccount(
                user.Id,
                user.Email,
                user.Email,
                role?.Name ?? "Unassigned",
                LoginStatus(user),
                new EmployeeSecurity(
                    user.LastPasswordChangedAtUtc,
                    user.LastLoginAtUtc,
                    user.AccessFailedCount,
                    user.MfaEnabled,
                    user.LockoutEndAtUtc is not null && user.LockoutEndAtUtc > dateTimeProvider.UtcNow,
                    activeSessions,
                    user.MustChangePassword));

        return new EmployeeEditor(
            employee.Id,
            employee.EmployeeNumber,
            employee.FirstName ?? string.Empty,
            employee.LastName ?? string.Empty,
            employee.DisplayName,
            employee.Email,
            employee.Phone,
            employee.JobTitle,
            employee.Department,
            employee.EmploymentType,
            employee.Status,
            employee.HireDate,
            employee.TerminationDate,
            loginAccount,
            permissionStates,
            activity);
    }

    private async Task<IReadOnlyCollection<EmployeeActivity>> GetActivityAsync(Guid organizationId, Guid employeeId, Guid? userId, CancellationToken cancellationToken)
    {
        var timeline = await dbContext.TimelineEvents.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.EntityType == "Employee" && x.EntityId == employeeId.ToString())
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(30)
            .Select(x => new EmployeeActivity(x.OccurredAtUtc, x.EventType, x.Summary))
            .ToListAsync(cancellationToken);
        if (userId is null)
        {
            return timeline;
        }

        var identity = await dbContext.TenantIdentityEvents.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.UserId == userId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(30)
            .Select(x => new EmployeeActivity(x.OccurredAtUtc, x.EventType, x.EventType))
            .ToListAsync(cancellationToken);

        return timeline.Concat(identity).OrderByDescending(x => x.OccurredAtUtc).Take(30).ToList();
    }

    private async Task CreateLoginAccountAsync(Guid organizationId, Employee employee, string email, string roleName, bool sendInvitationEmail, string? initialPassword, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalizedEmail = Required(email, "Email").ToUpperInvariant();
        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedEmail == normalizedEmail && x.DeletedAtUtc == null, cancellationToken))
        {
            throw new InvalidOperationException("A login account with this email already exists.");
        }

        if (!sendInvitationEmail && (string.IsNullOrWhiteSpace(initialPassword) || initialPassword.Length < 12))
        {
            throw new InvalidOperationException("Initial password must be at least 12 characters.");
        }

        var role = await GetRoleAsync(organizationId, roleName, cancellationToken);
        var user = new ApplicationUser
        {
            OrganizationId = organizationId,
            EmployeeId = employee.Id,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            DisplayName = employee.DisplayName,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            Phone = employee.Phone,
            JobTitle = employee.JobTitle,
            IsActive = employee.Status.Equals("Active", StringComparison.OrdinalIgnoreCase),
            DisabledAtUtc = employee.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? null : now,
            MustChangePassword = true,
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, sendInvitationEmail ? NewToken() : initialPassword!);
        if (!sendInvitationEmail)
        {
            user.LastPasswordChangedAtUtc = now;
        }

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        user.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            DisplayName = employee.DisplayName,
            EmployeeNumber = employee.EmployeeNumber,
            Status = sendInvitationEmail ? "Invited" : LoginStatus(user),
            IsActive = user.IsActive
        });
        dbContext.Users.Add(user);
        employee.LoginAccount = user;

        if (sendInvitationEmail)
        {
            var token = NewToken();
            dbContext.UserInvitations.Add(new UserInvitation
            {
                OrganizationId = organizationId,
                UserId = user.Id,
                Email = user.Email,
                TokenHash = TenantIdentityService.HashToken(token),
                ExpiresAtUtc = now.AddDays(7)
            });
            await welcomeEmailSender.SendAsync(user.Email, "You are invited to iM1 OS", $"Activate your company account: /company/activate?token={Uri.EscapeDataString(token)}", cancellationToken);
        }
    }

    private async Task EnsureCanManageEmployeesAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var isCompanyAdmin = await dbContext.UserRoles.IgnoreQueryFilters()
            .AnyAsync(x =>
                x.UserId == actorUserId &&
                x.Role != null &&
                x.Role.OrganizationId == organizationId &&
                (x.Role.NormalizedName == "OWNER" || x.Role.NormalizedName == "ADMINISTRATOR"),
                cancellationToken);
        if (isCompanyAdmin)
        {
            return;
        }

        var isPlatformAdministrator = await dbContext.PlatformUsers.AnyAsync(x => x.Id == actorUserId && x.IsActive && x.Role == "Platform Administrator", cancellationToken);
        if (!isPlatformAdministrator)
        {
            throw new UnauthorizedAccessException("Employees requires Owner or Administrator access.");
        }
    }

    private async Task EnsureDefaultRolesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Roles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.NormalizedName)
            .ToListAsync(cancellationToken);
        foreach (var roleName in DefaultRoleNames)
        {
            var normalized = NormalizeRole(roleName);
            if (existing.Contains(normalized))
            {
                continue;
            }

            dbContext.Roles.Add(new Role
            {
                OrganizationId = organizationId,
                Name = roleName,
                NormalizedName = normalized,
                IsSystemRole = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Role> GetRoleAsync(Guid organizationId, string roleName, CancellationToken cancellationToken)
    {
        await EnsureDefaultRolesAsync(organizationId, cancellationToken);
        var normalized = NormalizeRole(roleName);
        return await dbContext.Roles.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.NormalizedName == normalized, cancellationToken);
    }

    private async Task<Employee> LoadEmployeeAsync(Guid organizationId, Guid employeeId, CancellationToken cancellationToken)
    {
        return await dbContext.Employees.IgnoreQueryFilters()
            .Include(x => x.LoginAccount!).ThenInclude(x => x.UserRoles)
            .Include(x => x.LoginAccount!).ThenInclude(x => x.OrganizationMemberships)
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == employeeId && x.DeletedAtUtc == null, cancellationToken);
    }

    private void AddActivity(Guid organizationId, Guid employeeId, Guid actorUserId, string eventType, string summary, string? ipAddress, object payload)
    {
        var now = dateTimeProvider.UtcNow;
        var payloadJson = JsonSerializer.Serialize(payload);
        dbContext.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = actorUserId.ToString(),
            Action = eventType,
            EntityName = "Employee",
            EntityId = employeeId.ToString(),
            ChangesJson = payloadJson,
            OccurredAtUtc = now
        });
        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            EntityType = "Employee",
            EntityId = employeeId.ToString(),
            EventType = eventType,
            ActorUserId = actorUserId.ToString(),
            Summary = summary,
            OccurredAtUtc = now,
            PayloadJson = payloadJson
        });
    }

    private void AddIdentityEvent(Guid organizationId, Guid userId, string eventType, string? ipAddress, object payload)
    {
        dbContext.TenantIdentityEvents.Add(new TenantIdentityEvent
        {
            OrganizationId = organizationId,
            UserId = userId,
            EventType = eventType,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            IpAddress = ipAddress,
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }

    private static EmployeeRow ToRow(Employee employee)
    {
        var user = employee.LoginAccount;
        var role = user?.UserRoles.Select(x => x.Role?.Name).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "No Login";
        return new EmployeeRow(
            employee.Id,
            AvatarFor(employee.DisplayName),
            employee.DisplayName,
            employee.Email,
            employee.JobTitle,
            employee.Status,
            user is not null,
            user is null ? "No Login" : LoginStatus(user),
            role,
            user?.LastLoginAtUtc,
            employee.CreatedAtUtc);
    }

    private static string LoginStatus(ApplicationUser user)
    {
        if (user.LockoutEndAtUtc is not null && user.LockoutEndAtUtc > DateTimeOffset.UtcNow)
        {
            return "Locked";
        }

        if (!user.IsActive)
        {
            return "Disabled";
        }

        return "Active";
    }

    private static void SyncLoginFromEmployee(ApplicationUser user, Employee employee)
    {
        user.FirstName = employee.FirstName;
        user.LastName = employee.LastName;
        user.DisplayName = employee.DisplayName;
        if (!string.IsNullOrWhiteSpace(employee.Email))
        {
            user.Email = employee.Email;
            user.NormalizedEmail = employee.Email.ToUpperInvariant();
        }
        user.Phone = employee.Phone;
        user.JobTitle = employee.JobTitle;
        foreach (var membership in user.OrganizationMemberships.Where(x => x.OrganizationId == employee.OrganizationId))
        {
            membership.DisplayName = employee.DisplayName;
            membership.EmployeeNumber = employee.EmployeeNumber;
            membership.Status = LoginStatus(user);
            membership.IsActive = user.IsActive;
        }
    }

    private static void ValidateEmployeeRequest(CreateEmployeeRequest request)
    {
        _ = Required(request.Name, "Name");
        _ = Required(request.EmploymentType, "Employment type");
        _ = Required(request.Status, "Status");
        if (request.EnableLoginAccount)
        {
            _ = Required(request.Email ?? string.Empty, "Email");
            _ = Required(request.RoleName ?? string.Empty, "Company role");
            if (!request.SendInvitationEmail && (string.IsNullOrWhiteSpace(request.InitialPassword) || request.InitialPassword.Length < 12))
            {
                throw new InvalidOperationException("Initial password must be at least 12 characters.");
            }
        }
    }

    private static object Snapshot(Employee employee) => new
    {
        employee.EmployeeNumber,
        employee.FirstName,
        employee.LastName,
        employee.DisplayName,
        employee.Email,
        employee.Phone,
        employee.JobTitle,
        employee.Department,
        employee.EmploymentType,
        employee.Status,
        employee.HireDate,
        employee.TerminationDate
    };

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NormalizeRole(string value) => Required(value, "Role").Trim().ToUpperInvariant().Replace(' ', '_');

    private static string Required(string value, string fieldName) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{fieldName} is required.") : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FirstName(string name) => name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name.Trim();

    private static string? LastName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;
    }

    private static string AvatarFor(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(x => x[0])).ToUpperInvariant();
    }
}
