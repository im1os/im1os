using iM1os.Application.Authentication;
using System.Net;
using iM1os.Application.BusinessAdministration;
using iM1os.Application.Common;
using iM1os.Application.CompanySuppliers;
using iM1os.Application.Configuration;
using iM1os.Application.Customers;
using iM1os.Application.Employees;
using iM1os.Application.FinancialServices.Merchant;
using iM1os.Application.FinancialServices.Payments;
using iM1os.Application.FinancialServices.Providers;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.Inventory;
using iM1os.Application.Marketing;
using iM1os.Application.Payments;
using iM1os.Application.Platform;
using iM1os.Application.Tenancy;
using iM1os.Application.TenantIdentity;
using iM1os.Application.WorkOrders;
using iM1os.Domain.Identity;
using iM1os.Domain.Platform;
using iM1os.Infrastructure.Configuration;
using iM1os.Infrastructure.FinancialServices.Providers;
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
        services.Configure<NmiPaymentOptions>(configuration.GetSection(NmiPaymentOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        var redisConnection = configuration.GetConnectionString("Redis");
        services.AddMemoryCache();
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
        services.AddScoped<IPlatformOperationsService, PlatformOperationsService>();
        services.AddScoped<ITenantModuleEntitlementService, TenantModuleEntitlementService>();
        services.AddScoped<IPlatformSupplierConnectorService, PlatformSupplierConnectorService>();
        services.AddScoped<ICompanySupplierService, CompanySupplierService>();
        services.AddScoped<IWpsDealerPricingImportService, WpsDealerPricingImportService>();
        services.AddScoped<IPartsUnlimitedDealerPricingImportService, PartsUnlimitedDealerPricingImportService>();
        services.AddScoped<ITurn14DealerPricingImportService, Turn14DealerPricingImportService>();
        services.AddScoped<IWpsMasterItemListImportService, WpsMasterItemListImportService>();
        services.AddScoped<ITurn14ProductLoadsheetImportService, Turn14ProductLoadsheetImportService>();
        services.AddScoped<ITurn14MediaEnrichmentService, Turn14MediaEnrichmentService>();
        services.AddScoped<IPartsUnlimitedBundleImportService, PartsUnlimitedBundleImportService>();
        services.AddScoped<IPartsUnlimitedBrandImageImportService, PartsUnlimitedBundleImportService>();
        services.AddScoped<ICatalogTireBackfillService, CatalogTireBackfillService>();
        services.AddScoped<ICatalogNormalizationService, CatalogNormalizationService>();
        services.AddScoped<ISupplierItemSearchService, SupplierItemSearchService>();
        services.AddScoped<IWpsLiveInventoryService, WpsLiveInventoryService>();
        services.AddScoped<ITurn14LiveInventoryService, Turn14LiveInventoryService>();
        services.AddScoped<IPartsUnlimitedLiveInventoryService, PartsUnlimitedLiveInventoryService>();
        services.AddScoped<IIndieMotoFitmentImportService, IndieMotoFitmentImportService>();
        services.AddScoped<ITenantIdentityService, TenantIdentityService>();
        services.AddScoped<IBusinessOnboardingService, BusinessOnboardingService>();
        services.AddScoped<IBusinessAdministrationService, BusinessAdministrationService>();
        services.AddScoped<ICustomerCrmService, CustomerCrmService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<ICompanyInventoryService, CompanyInventoryService>();
        services.AddScoped<IMarketingCmsService, MarketingCmsService>();
        services.AddScoped<IMerchantAccountService, MerchantAccountService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IIm1PaymentsService>(sp => sp.GetRequiredService<IPaymentService>() as IIm1PaymentsService
            ?? throw new InvalidOperationException("Payment service must implement the legacy iM1 payments facade."));
        services.AddScoped<IPaymentProvider, NmiPaymentProvider>();
        services.AddScoped<IPartnerProvider, NmiPartnerProvider>();
        services.AddScoped<IMerchantProvider, NmiMerchantProvider>();
        services.AddScoped<ITerminalProvider, NmiTerminalProvider>();
        services.AddScoped<ICustomerVaultProvider, NmiCustomerVaultProvider>();
        services.AddScoped<IACHProvider, NmiAchProvider>();
        services.AddScoped<ISubscriptionProvider, NmiSubscriptionProvider>();
        services.AddScoped<ITenantProfileService, TenantProfileService>();
        services.AddScoped<IWelcomeEmailSender, NoOpWelcomeEmailSender>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ICurrentUser, NoCurrentUser>();
        services.AddSingleton<IDateTimeProvider, SystemClock>();
        services.AddHttpClient("WpsDataDepot", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-WPS-Importer/1.0");
        });
        services.AddHttpClient("IndieMotoFitment", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-Fitment-Importer/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });
        services.AddHttpClient("Turn14Api", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-Turn14-Api/1.0");
        });
        services.AddHttpClient("PartsUnlimitedApi", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-PartsUnlimited-Importer/1.0");
        });
        services.AddHttpClient("NmiPayments", client =>
        {
            var options = configuration.GetSection(NmiPaymentOptions.SectionName).Get<NmiPaymentOptions>() ?? new NmiPaymentOptions();
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.PaymentsBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-NMI-Payments/1.0");
        });
        services.AddHttpClient("NmiPartner", client =>
        {
            var options = configuration.GetSection(NmiPaymentOptions.SectionName).Get<NmiPaymentOptions>() ?? new NmiPaymentOptions();
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.AccountManagementBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-NMI-Partner/1.0");
        });
        services.AddHttpClient("NmiSignup", client =>
        {
            var options = configuration.GetSection(NmiPaymentOptions.SectionName).Get<NmiPaymentOptions>() ?? new NmiPaymentOptions();
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.SignUpBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("iM1os-NMI-Signup/1.0");
        });
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<IPasswordHasher<PlatformUser>, PasswordHasher<PlatformUser>>();

        return services;
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }
}
