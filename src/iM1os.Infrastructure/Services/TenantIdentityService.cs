using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Application.TenantIdentity;
using iM1os.Domain.Audit;
using iM1os.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class TenantIdentityService(
    IApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IWelcomeEmailSender welcomeEmailSender,
    IDateTimeProvider dateTimeProvider) : ITenantIdentityService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<TenantLoginResult?> LoginAsync(TenantLoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var query = dbContext.Users.IgnoreQueryFilters().AsQueryable();

        if (request.OrganizationId.HasValue)
        {
            query = query.Where(x => x.OrganizationId == request.OrganizationId.Value);
        }

        var matches = await query
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x!.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .Take(2)
            .ToListAsync(cancellationToken);
        var user = matches.Count == 1 ? matches[0] : null;

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var now = dateTimeProvider.UtcNow;
        if (user.LockoutEndAtUtc.HasValue && user.LockoutEndAtUtc > now)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAttempts)
            {
                user.LockoutEndAtUtc = now.Add(LockoutDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.AccessFailedCount = 0;
        user.LockoutEndAtUtc = null;
        user.LastLoginAtUtc = now;
        AddSession(user, ipAddress, now);
        AddEvent(user.OrganizationId, user.Id, "UserLoggedIn", ipAddress, new { user.Email });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildLoginResultAsync(user, cancellationToken);
    }

    public async Task<TenantLoginResult?> ActivateOwnerAsync(ActivateOwnerRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        if (request.Password != request.ConfirmPassword || request.Password.Length < 12)
        {
            return null;
        }

        var tokenHash = HashToken(request.Token);
        var now = dateTimeProvider.UtcNow;
        var invitation = await dbContext.UserInvitations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x =>
                x.TokenHash == tokenHash &&
                x.AcceptedAtUtc == null &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > now,
                cancellationToken);

        if (invitation is null)
        {
            return null;
        }

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x!.RolePermissions)
            .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == invitation.UserId && x.OrganizationId == invitation.OrganizationId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.IsActive = true;
        user.MustChangePassword = false;
        user.EmailVerifiedAtUtc = now;
        user.AccessFailedCount = 0;
        user.LockoutEndAtUtc = null;
        user.LastLoginAtUtc = now;
        user.LastPasswordChangedAtUtc = now;
        invitation.AcceptedAtUtc = now;

        AddSession(user, ipAddress, now);
        AddEvent(user.OrganizationId, user.Id, "OwnerActivated", ipAddress, new { user.Email });
        AddEvent(user.OrganizationId, user.Id, "UserLoggedIn", ipAddress, new { user.Email });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildLoginResultAsync(user, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(PasswordResetRequestDto request, string? ipAddress, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var query = dbContext.Users.IgnoreQueryFilters().Where(x => x.NormalizedEmail == normalizedEmail && x.IsActive);
        if (request.OrganizationId.HasValue)
        {
            query = query.Where(x => x.OrganizationId == request.OrganizationId.Value);
        }

        var user = await query.SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return;
        }

        var token = NewToken();
        dbContext.PasswordResetRequests.Add(new PasswordResetRequest
        {
            OrganizationId = user.OrganizationId,
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresAtUtc = dateTimeProvider.UtcNow.AddHours(2)
        });

        AddEvent(user.OrganizationId, user.Id, "PasswordResetRequested", ipAddress, new { user.Email });
        await welcomeEmailSender.SendAsync(
            user.Email,
            "Reset your IM1OS password",
            $"Reset your IM1OS password: /company/reset-password?token={Uri.EscapeDataString(token)}",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CompletePasswordResetAsync(CompletePasswordResetRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        if (request.Password != request.ConfirmPassword || request.Password.Length < 12)
        {
            return false;
        }

        var tokenHash = HashToken(request.Token);
        var now = dateTimeProvider.UtcNow;
        var reset = await dbContext.PasswordResetRequests
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.CompletedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);

        if (reset is null)
        {
            return false;
        }

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == reset.UserId && x.OrganizationId == reset.OrganizationId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.MustChangePassword = false;
        user.AccessFailedCount = 0;
        user.LockoutEndAtUtc = null;
        user.LastPasswordChangedAtUtc = now;
        reset.CompletedAtUtc = now;

        AddEvent(user.OrganizationId, user.Id, "PasswordResetCompleted", ipAddress, new { user.Email });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task LogoutAsync(Guid organizationId, Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var sessions = await dbContext.UserSessions.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAtUtc = now;
        }

        AddEvent(organizationId, userId, "UserLoggedOut", ipAddress, new { });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string NewToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<TenantLoginResult> BuildLoginResultAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == user.OrganizationId, cancellationToken);
        var onboardingComplete = await dbContext.BusinessOnboardings
            .IgnoreQueryFilters()
            .AnyAsync(x => x.OrganizationId == user.OrganizationId && x.CompletedAtUtc != null, cancellationToken);

        var roles = user.UserRoles.Select(x => x.Role?.Name).Where(x => x is not null).Select(x => x!).Distinct().Order().ToArray();
        var permissionSet = user.UserRoles
            .SelectMany(x => x.Role?.RolePermissions ?? [])
            .Select(x => x.Permission?.Key)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overrides = await dbContext.UserPermissionOverrides
            .IgnoreQueryFilters()
            .Include(x => x.Permission)
            .Where(x => x.OrganizationId == user.OrganizationId && x.UserId == user.Id)
            .ToListAsync(cancellationToken);
        foreach (var permissionOverride in overrides.Where(x => x.Permission is not null))
        {
            if (permissionOverride.IsAllowed)
            {
                permissionSet.Add(permissionOverride.Permission!.Key);
            }
            else
            {
                permissionSet.Remove(permissionOverride.Permission!.Key);
            }
        }

        var permissions = permissionSet.Order().ToArray();

        return new TenantLoginResult(
            user.Id,
            user.OrganizationId,
            user.Email,
            user.DisplayName,
            organization.Name,
            organization.LogoUrl,
            roles,
            permissions,
            roles.Contains("Owner", StringComparer.OrdinalIgnoreCase) && !onboardingComplete);
    }

    private void AddEvent(Guid organizationId, Guid? userId, string eventType, string? ipAddress, object payload)
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

    private void AddSession(ApplicationUser user, string? ipAddress, DateTimeOffset now)
    {
        dbContext.UserSessions.Add(new UserSession
        {
            OrganizationId = user.OrganizationId,
            UserId = user.Id,
            SessionKey = NewToken(),
            IpAddress = ipAddress,
            StartedAtUtc = now,
            LastSeenAtUtc = now
        });
    }
}
