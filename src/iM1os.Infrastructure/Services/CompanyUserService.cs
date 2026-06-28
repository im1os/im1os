using System.Security.Cryptography;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.CompanyUsers;
using iM1os.Application.Platform;
using iM1os.Domain.Audit;
using iM1os.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class CompanyUserService(
    IApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IWelcomeEmailSender welcomeEmailSender,
    IDateTimeProvider dateTimeProvider) : ICompanyUserService
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

    public async Task<CompanyUsersWorkspace> GetWorkspaceAsync(Guid organizationId, Guid actorUserId, CompanyUserSearchRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageUsersAsync(organizationId, actorUserId, cancellationToken);
        await EnsureDefaultRolesAsync(organizationId, cancellationToken);

        var roles = await dbContext.Roles.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new CompanyRoleOption(x.Id, x.Name, x.IsSystemRole))
            .ToListAsync(cancellationToken);
        var permissions = await dbContext.Permissions
            .OrderBy(x => x.Key)
            .Select(x => new PermissionDefinitionDto(x.Id, x.Key, x.Description))
            .ToListAsync(cancellationToken);

        var query = dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.DeletedAtUtc == null)
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var search = request.Query.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.NormalizedEmail.Contains(search) ||
                x.DisplayName.ToUpper().Contains(search) ||
                (x.JobTitle != null && x.JobTitle.ToUpper().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => UserStatus(x) == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(x => x.UserRoles.Any(role => role.Role != null && role.Role.Name == request.Role));
        }

        var users = await query
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        var rows = users.Select(ToRow).ToList();
        var selectedUser = request.SelectedUserId is Guid userId
            ? await GetEditorAsync(organizationId, userId, permissions, cancellationToken)
            : null;

        return new CompanyUsersWorkspace(rows, roles, permissions, selectedUser, request.Query, request.Status, request.Role);
    }

    public async Task<Guid> CreateUserAsync(Guid organizationId, Guid actorUserId, CreateCompanyUserRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageUsersAsync(organizationId, actorUserId, cancellationToken);
        ValidateCreateRequest(request);
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedEmail == normalizedEmail && x.DeletedAtUtc == null, cancellationToken))
        {
            throw new InvalidOperationException("A company user with this email already exists.");
        }

        var role = await GetRoleAsync(organizationId, request.RoleName, cancellationToken);
        var now = dateTimeProvider.UtcNow;
        var user = new ApplicationUser
        {
            OrganizationId = organizationId,
            FirstName = FirstName(request.Name),
            LastName = LastName(request.Name),
            DisplayName = request.Name.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            IsActive = request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase),
            DisabledAtUtc = request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? null : now,
            MustChangePassword = true,
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.SendInvitationEmail ? NewToken() : request.InitialPassword!);
        if (!request.SendInvitationEmail)
        {
            user.LastPasswordChangedAtUtc = now;
        }

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        user.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Status = request.SendInvitationEmail ? "Invited" : UserStatus(user),
            IsActive = user.IsActive
        });
        dbContext.Users.Add(user);

        if (request.SendInvitationEmail)
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

        AddActivity(organizationId, user.Id, actorUserId, "UserCreated", "User created", ipAddress, new { user.Email, user.DisplayName, Role = role.Name });
        await dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task UpdateUserAsync(Guid organizationId, Guid actorUserId, UpdateCompanyUserRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageUsersAsync(organizationId, actorUserId, cancellationToken);
        var user = await LoadUserAsync(organizationId, request.UserId, cancellationToken);
        var role = await GetRoleAsync(organizationId, request.RoleName, cancellationToken);
        var before = Snapshot(user);

        user.FirstName = Clean(request.FirstName);
        user.LastName = Clean(request.LastName);
        user.DisplayName = Required(request.DisplayName, "Display name");
        user.Email = Required(request.Email, "Email");
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.Phone = Clean(request.Phone);
        user.JobTitle = Clean(request.JobTitle);
        SetStatus(user, request.Status);

        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        foreach (var membership in user.OrganizationMemberships.Where(x => x.OrganizationId == organizationId))
        {
            membership.DisplayName = user.DisplayName;
            membership.Status = UserStatus(user);
            membership.IsActive = user.IsActive;
        }

        AddActivity(organizationId, user.Id, actorUserId, "ProfileUpdated", "Profile updated", ipAddress, new { before, after = Snapshot(user), Role = role.Name });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SavePermissionOverridesAsync(Guid organizationId, Guid actorUserId, SaveCompanyUserPermissionOverridesRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageUsersAsync(organizationId, actorUserId, cancellationToken);
        var user = await LoadUserAsync(organizationId, request.UserId, cancellationToken);
        var existing = await dbContext.UserPermissionOverrides.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.UserId == request.UserId)
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
                    UserId = request.UserId,
                    PermissionId = item.PermissionId
                };
                dbContext.UserPermissionOverrides.Add(permissionOverride);
            }

            permissionOverride.IsAllowed = item.IsAllowed;
        }

        AddActivity(organizationId, user.Id, actorUserId, "PermissionsChanged", "Permissions changed", ipAddress, request.Overrides);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RunSecurityActionAsync(Guid organizationId, Guid actorUserId, CompanyUserSecurityActionRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await EnsureCanManageUsersAsync(organizationId, actorUserId, cancellationToken);
        var user = await LoadUserAsync(organizationId, request.UserId, cancellationToken);
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
                AddActivity(organizationId, user.Id, actorUserId, "PasswordChanged", "Password changed", ipAddress, new { });
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
                AddActivity(organizationId, user.Id, actorUserId, "PasswordReset", "Password reset email sent", ipAddress, new { });
                break;
            case "UnlockAccount":
                user.AccessFailedCount = 0;
                user.LockoutEndAtUtc = null;
                AddActivity(organizationId, user.Id, actorUserId, "AccountUnlocked", "Account unlocked", ipAddress, new { });
                break;
            case "ForcePasswordChange":
                user.MustChangePassword = true;
                AddActivity(organizationId, user.Id, actorUserId, "ForcePasswordChange", "Password change required", ipAddress, new { });
                break;
            case "SignOutAllSessions":
                var sessions = await dbContext.UserSessions.IgnoreQueryFilters()
                    .Where(x => x.OrganizationId == organizationId && x.UserId == user.Id && x.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);
                foreach (var session in sessions)
                {
                    session.RevokedAtUtc = now;
                }
                AddActivity(organizationId, user.Id, actorUserId, "SessionsRevoked", "All sessions signed out", ipAddress, new { Count = sessions.Count });
                break;
            case "DisableUser":
                SetStatus(user, "Disabled");
                AddActivity(organizationId, user.Id, actorUserId, "UserDisabled", "User disabled", ipAddress, new { });
                break;
            case "EnableUser":
                SetStatus(user, "Active");
                AddActivity(organizationId, user.Id, actorUserId, "UserEnabled", "User enabled", ipAddress, new { });
                break;
            default:
                throw new InvalidOperationException("Unsupported security action.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompanyUserEditor?> GetEditorAsync(Guid organizationId, Guid userId, IReadOnlyCollection<PermissionDefinitionDto> permissions, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == userId && x.DeletedAtUtc == null)
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .Include(x => x.PermissionOverrides).ThenInclude(x => x.Permission)
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var role = user.UserRoles.Select(x => x.Role).FirstOrDefault(x => x is not null);
        var rolePermissionIds = role?.RolePermissions.Select(x => x.PermissionId).ToHashSet() ?? [];
        var overrideByPermission = user.PermissionOverrides.ToDictionary(x => x.PermissionId, x => x.IsAllowed);
        var permissionStates = permissions
            .Select(x => new CompanyUserPermissionState(
                x.Id,
                x.Key,
                x.Description,
                rolePermissionIds.Contains(x.Id),
                overrideByPermission.TryGetValue(x.Id, out var allowed) ? allowed : null as bool?))
            .ToList();
        var activity = await GetActivityAsync(organizationId, user.Id, cancellationToken);
        var activeSessions = user.Sessions.Count(x => x.RevokedAtUtc == null);

        return new CompanyUserEditor(
            user.Id,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            user.DisplayName,
            user.Email,
            user.Phone,
            user.JobTitle,
            role?.Name ?? "Unassigned",
            UserStatus(user),
            new CompanyUserSecurity(
                user.Email,
                user.Email,
                user.LastPasswordChangedAtUtc,
                user.LastLoginAtUtc,
                user.AccessFailedCount,
                user.MfaEnabled,
                user.LockoutEndAtUtc is not null && user.LockoutEndAtUtc > dateTimeProvider.UtcNow,
                activeSessions,
                user.MustChangePassword),
            permissionStates,
            activity);
    }

    private async Task<IReadOnlyCollection<CompanyUserActivity>> GetActivityAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var timeline = await dbContext.TimelineEvents.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.EntityType == "ApplicationUser" && x.EntityId == userId.ToString())
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(30)
            .Select(x => new CompanyUserActivity(x.OccurredAtUtc, x.EventType, x.Summary))
            .ToListAsync(cancellationToken);
        var identity = await dbContext.TenantIdentityEvents.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.UserId == userId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(30)
            .Select(x => new CompanyUserActivity(x.OccurredAtUtc, x.EventType, x.EventType))
            .ToListAsync(cancellationToken);

        return timeline.Concat(identity).OrderByDescending(x => x.OccurredAtUtc).Take(30).ToList();
    }

    private async Task EnsureCanManageUsersAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Company Users requires Owner or Administrator access.");
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

    private async Task<ApplicationUser> LoadUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.IgnoreQueryFilters()
            .Include(x => x.UserRoles)
            .Include(x => x.OrganizationMemberships)
            .SingleAsync(x => x.OrganizationId == organizationId && x.Id == userId && x.DeletedAtUtc == null, cancellationToken);
    }

    private void AddActivity(Guid organizationId, Guid userId, Guid actorUserId, string eventType, string summary, string? ipAddress, object payload)
    {
        var now = dateTimeProvider.UtcNow;
        var payloadJson = JsonSerializer.Serialize(payload);
        dbContext.AuditLogs.Add(new AuditLog
        {
            OrganizationId = organizationId,
            UserId = actorUserId.ToString(),
            Action = eventType,
            EntityName = "ApplicationUser",
            EntityId = userId.ToString(),
            ChangesJson = payloadJson,
            OccurredAtUtc = now
        });
        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            EntityType = "ApplicationUser",
            EntityId = userId.ToString(),
            EventType = eventType,
            ActorUserId = actorUserId.ToString(),
            Summary = summary,
            OccurredAtUtc = now,
            PayloadJson = payloadJson
        });
        dbContext.TenantIdentityEvents.Add(new TenantIdentityEvent
        {
            OrganizationId = organizationId,
            UserId = userId,
            EventType = eventType,
            OccurredAtUtc = now,
            IpAddress = ipAddress,
            PayloadJson = payloadJson
        });
    }

    private static CompanyUserRow ToRow(ApplicationUser user)
    {
        var role = user.UserRoles.Select(x => x.Role?.Name).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Unassigned";
        return new CompanyUserRow(
            user.Id,
            AvatarFor(user.DisplayName),
            user.DisplayName,
            user.Email,
            role,
            UserStatus(user),
            user.LastLoginAtUtc,
            user.MfaEnabled,
            user.CreatedAtUtc);
    }

    private static string UserStatus(ApplicationUser user)
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

    private void SetStatus(ApplicationUser user, string status)
    {
        if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            user.IsActive = true;
            user.DisabledAtUtc = null;
            return;
        }

        user.IsActive = false;
        user.DisabledAtUtc ??= dateTimeProvider.UtcNow;
    }

    private static void ValidateCreateRequest(CreateCompanyUserRequest request)
    {
        _ = Required(request.Name, "Name");
        _ = Required(request.Email, "Email");
        _ = Required(request.RoleName, "Company role");
        if (!request.SendInvitationEmail && (string.IsNullOrWhiteSpace(request.InitialPassword) || request.InitialPassword.Length < 12))
        {
            throw new InvalidOperationException("Initial password must be at least 12 characters.");
        }
    }

    private static object Snapshot(ApplicationUser user) => new
    {
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.Email,
        user.Phone,
        user.JobTitle,
        Status = UserStatus(user)
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
