using iM1os.Domain.Configuration;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Domain.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace iM1os.Infrastructure.Persistence;

public sealed class ApplicationDbInitializer(
    ApplicationDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IPasswordHasher<PlatformUser> platformPasswordHasher,
    ILogger<ApplicationDbInitializer> logger) : IApplicationDbInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.PlatformUsers.AnyAsync(cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding initial iM1 OS platform data.");

        var platformUser = new PlatformUser
        {
            Email = "admin@im1os.com",
            NormalizedEmail = "admin@im1os.com".ToUpperInvariant(),
            DisplayName = "Platform Administrator",
            PasswordHash = string.Empty,
            Role = "Platform Administrator"
        };
        platformUser.PasswordHash = platformPasswordHasher.HashPassword(platformUser, "ChangeMe!12345");

        dbContext.PlatformUsers.Add(platformUser);

        if (await dbContext.Organizations.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var organization = new Organization { Name = "iM1 OS", Slug = "im1os" };
        var permissions = new[]
        {
            new Permission { Key = "platform.manage", Description = "Manage platform configuration." },
            new Permission { Key = "users.manage", Description = "Manage users and access." },
            new Permission { Key = "organizations.manage", Description = "Manage organizations." }
        };

        var adminRole = new Role
        {
            OrganizationId = organization.Id,
            Name = "Platform Administrator",
            NormalizedName = "PLATFORM ADMINISTRATOR",
            IsSystemRole = true
        };

        foreach (var permission in permissions)
        {
            adminRole.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permission.Id });
        }

        var user = new ApplicationUser
        {
            OrganizationId = organization.Id,
            Email = "admin@im1os.com",
            NormalizedEmail = "admin@im1os.com".ToUpperInvariant(),
            DisplayName = "Platform Administrator",
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "ChangeMe!12345");
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });

        dbContext.Organizations.Add(organization);
        dbContext.Permissions.AddRange(permissions);
        dbContext.Roles.Add(adminRole);
        dbContext.Users.Add(user);
        dbContext.FeatureFlags.Add(new FeatureFlag { Key = "dashboard.shell", IsEnabled = true, Description = "Enables the initial dashboard shell." });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
