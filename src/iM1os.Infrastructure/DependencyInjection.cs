using iM1os.Application.Authentication;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.Common;
using iM1os.Application.Configuration;
using iM1os.Application.Platform;
using iM1os.Application.Tenancy;
using iM1os.Application.TenantIdentity;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Security;
using iM1os.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace iM1os.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbInitializer, ApplicationDbInitializer>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddScoped<IDomainEventRecorder, DomainEventRecorder>();
        services.AddScoped<IPlatformAuthenticationService, PlatformAuthenticationService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ITenantManagerService, TenantManagerService>();
        services.AddScoped<ITenantIdentityService, TenantIdentityService>();
        services.AddScoped<IBusinessOnboardingService, BusinessOnboardingService>();
        services.AddScoped<IBusinessAdministrationService, BusinessAdministrationService>();
        services.AddScoped<ITenantProfileService, TenantProfileService>();
        services.AddScoped<IWelcomeEmailSender, NoOpWelcomeEmailSender>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ICurrentUser, NoCurrentUser>();
        services.AddSingleton<IDateTimeProvider, SystemClock>();
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<IPasswordHasher<PlatformUser>, PasswordHasher<PlatformUser>>();

        return services;
    }
}
