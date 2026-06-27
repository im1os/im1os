using iM1os.Application.Common;
using iM1os.Application.Platform;
using iM1os.Domain.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class PlatformAuthenticationService(
    IApplicationDbContext dbContext,
    IPasswordHasher<PlatformUser> passwordHasher,
    IDateTimeProvider dateTimeProvider) : IPlatformAuthenticationService
{
    public async Task<PlatformLoginResult?> LoginAsync(PlatformLoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await dbContext.PlatformUsers.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && x.IsActive, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        user.LastLoginAtUtc = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlatformLoginResult(user.Id, user.Email, user.DisplayName, user.Role);
    }
}
