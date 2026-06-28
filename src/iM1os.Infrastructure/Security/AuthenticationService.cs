using iM1os.Application.Authentication;
using iM1os.Application.Common;
using iM1os.Domain.Employees;
using iM1os.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Security;

public sealed class AuthenticationService(
    IApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider) : IAuthenticationService
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var query = dbContext.Users.AsQueryable();

        if (request.OrganizationId.HasValue)
        {
            query = query.Where(x => x.OrganizationId == request.OrganizationId.Value);
        }

        var user = await query
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x!.RolePermissions)
            .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && x.IsActive, cancellationToken);

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

        var roles = user.UserRoles.Select(x => x.Role?.Name).Where(x => x is not null).Select(x => x!).Distinct().Order().ToArray();
        var permissions = user.UserRoles
            .SelectMany(x => x.Role?.RolePermissions ?? [])
            .Select(x => x.Permission?.Key)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct()
            .Order()
            .ToArray();

        return jwtTokenService.CreateToken(user, roles, permissions);
    }

    public async Task<Result> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.OrganizationId == request.OrganizationId && x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (exists)
        {
            return Result.Failure("A user with this email already exists in the organization.");
        }

        var employee = new Employee
        {
            OrganizationId = request.OrganizationId,
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Status = "Active"
        };
        var user = new ApplicationUser
        {
            OrganizationId = request.OrganizationId,
            EmployeeId = employee.Id,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var normalizedRoles = request.RoleNames.Select(x => x.Trim().ToUpperInvariant()).ToArray();
        var roles = await dbContext.Roles.Where(x => normalizedRoles.Contains(x.NormalizedName)).ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        user.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = request.OrganizationId,
            UserId = user.Id,
            DisplayName = request.DisplayName.Trim()
        });

        dbContext.Employees.Add(employee);
        dbContext.Users.Add(user);
        employee.LoginAccount = user;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
