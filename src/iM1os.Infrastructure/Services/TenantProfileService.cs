using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.TenantIdentity;
using iM1os.Domain.Audit;
using iM1os.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class TenantProfileService(
    IApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IDateTimeProvider dateTimeProvider) : ITenantProfileService
{
    public async Task<TenantProfile?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Id == userId)
            .Select(x => new TenantProfile(x.DisplayName, x.Email, x.Phone, x.Language, x.TimeZone))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateAsync(Guid organizationId, Guid userId, UpdateTenantProfileRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == organizationId && x.Id == userId, cancellationToken);
        user.DisplayName = request.Name.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.Language = request.Language.Trim();
        user.TimeZone = request.TimeZone.Trim();
        AddEvent(organizationId, userId, "ProfileUpdated", ipAddress, new { user.Email });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(Guid organizationId, Guid userId, ChangePasswordRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmPassword || request.NewPassword.Length < 12)
        {
            return false;
        }

        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.OrganizationId == organizationId && x.Id == userId, cancellationToken);
        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        user.LastPasswordChangedAtUtc = dateTimeProvider.UtcNow;
        AddEvent(organizationId, userId, "PasswordChanged", ipAddress, new { user.Email });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void AddEvent(Guid organizationId, Guid userId, string eventType, string? ipAddress, object payload)
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
}
