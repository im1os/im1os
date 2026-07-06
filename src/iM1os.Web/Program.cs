using iM1os.Application.Common;
using iM1os.Infrastructure;
using iM1os.Infrastructure.Configuration;
using iM1os.Infrastructure.Persistence;
using iM1os.Web.Security;
using iM1os.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddLocalDotEnvFile();

builder.Host.UseSerilog((context, logger) =>
{
    logger.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/company/login";
        options.AccessDeniedPath = "/platform/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            var loginPath = context.Request.Path.StartsWithSegments("/admin") ||
                context.Request.Path.StartsWithSegments("/platform") ||
                context.Request.Path.StartsWithSegments("/Platform")
                ? "/platform/login"
                : "/company/login";
            context.Response.Redirect($"{loginPath}?ReturnUrl={Uri.EscapeDataString(context.Request.PathBase + context.Request.Path + context.Request.QueryString)}");
            return Task.CompletedTask;
        };
    });
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("postgresql");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseSerilogRequestLogging();
var uploadRoot = DocumentUploadStorage.UploadRoot(app.Environment);
Directory.CreateDirectory(uploadRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadRoot),
    RequestPath = "/uploads"
});
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var queryString = context.Request.QueryString.Value ?? string.Empty;
        var organizationId = context.Request.Query["organizationId"].FirstOrDefault();
        string? redirectTo = path.ToLowerInvariant() switch
        {
            "/admin" => "/platform",
            "/admin/login" => "/platform/login",
            "/admin/dashboard" => "/platform",
            "/admin/tenants" => $"/platform/companies{queryString}",
            "/admin/createtenant" => "/platform/companies/create",
            "/admin/tenant" when !string.IsNullOrWhiteSpace(organizationId) => $"/platform/companies/{organizationId}",
            "/admin/edittenant" when !string.IsNullOrWhiteSpace(organizationId) => $"/platform/companies/{organizationId}/edit",
            "/admin/provisioned" when !string.IsNullOrWhiteSpace(organizationId) => $"/platform/companies/{organizationId}/provisioned",
            "/account/login" => $"/company/login{queryString}",
            "/account/forgotpassword" => "/company/forgot-password",
            "/account/resetpassword" => $"/company/reset-password{queryString}",
            "/account/activate" => $"/company/activate{queryString}",
            "/business/dashboard" => $"/company{queryString}",
            "/business/administration" => $"/company/admin{queryString}",
            "/company/users" => $"/company/employees{queryString}",
            "/profile" => "/company/profile",
            "/profile/index" => "/company/profile",
            "/onboarding" => "/company/setup",
            "/onboarding/index" => "/company/setup",
            _ => null
        };

        if (redirectTo is not null)
        {
            context.Response.Redirect(redirectTo, permanent: false);
            return;
        }
    }

    await next();
});
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "platform-login",
    pattern: "platform/login",
    defaults: new { controller = "Platform", action = "Login" });
app.MapControllerRoute(
    name: "platform-logout",
    pattern: "platform/logout",
    defaults: new { controller = "Platform", action = "Logout" });
app.MapControllerRoute(
    name: "platform-dashboard",
    pattern: "platform",
    defaults: new { controller = "Platform", action = "Dashboard" });
app.MapControllerRoute(
    name: "platform-supplier-search",
    pattern: "platform/suppliers/catalog/item-search",
    defaults: new { controller = "Platform", action = "ItemSearch" });
app.MapControllerRoute(
    name: "platform-supplier-search-results",
    pattern: "platform/suppliers/catalog/item-search/results",
    defaults: new { controller = "Platform", action = "ItemSearchResults" });
app.MapControllerRoute(
    name: "platform-supplier-wps",
    pattern: "platform/suppliers/wps",
    defaults: new { controller = "Platform", action = "WpsConnector" });
app.MapControllerRoute(
    name: "platform-supplier-turn14",
    pattern: "platform/suppliers/turn14",
    defaults: new { controller = "Platform", action = "Turn14Connector" });
app.MapControllerRoute(
    name: "platform-supplier-parts-unlimited",
    pattern: "platform/suppliers/parts-unlimited",
    defaults: new { controller = "Platform", action = "PartsUnlimitedConnector" });
app.MapControllerRoute(
    name: "platform-supplier-scheduler",
    pattern: "platform/suppliers/scheduler",
    defaults: new { controller = "Platform", action = "Scheduler" });
app.MapControllerRoute(
    name: "platform-operations",
    pattern: "platform/operations",
    defaults: new { controller = "Platform", action = "Operations" });
app.MapControllerRoute(
    name: "platform-financial-services-module",
    pattern: "platform/financial-services/modules/{moduleKey}",
    defaults: new { controller = "PlatformFinancialServices", action = "Module" });
app.MapControllerRoute(
    name: "platform-financial-services",
    pattern: "platform/financial-services/{action=Index}",
    defaults: new { controller = "PlatformFinancialServices" });
app.MapControllerRoute(
    name: "platform-supplier-wps-inventory",
    pattern: "platform/suppliers/wps/inventory",
    defaults: new { controller = "Platform", action = "WpsInventory" });
app.MapControllerRoute(
    name: "platform-supplier-turn14-inventory",
    pattern: "platform/suppliers/turn14/inventory",
    defaults: new { controller = "Platform", action = "Turn14Inventory" });
app.MapControllerRoute(
    name: "platform-supplier-parts-unlimited-inventory",
    pattern: "platform/suppliers/parts-unlimited/inventory",
    defaults: new { controller = "Platform", action = "PartsUnlimitedInventory" });
app.MapControllerRoute(
    name: "platform-supplier-fitment",
    pattern: "platform/suppliers/catalog/fitment",
    defaults: new { controller = "Platform", action = "FetchItemFitment" });
app.MapControllerRoute(
    name: "platform-marketing",
    pattern: "platform/marketing/{action=Index}/{id?}",
    defaults: new { controller = "MarketingAdmin" });
app.MapControllerRoute(
    name: "platform-companies",
    pattern: "platform/companies",
    defaults: new { controller = "Platform", action = "Tenants" });
app.MapControllerRoute(
    name: "platform-company-create",
    pattern: "platform/companies/create",
    defaults: new { controller = "Platform", action = "CreateTenant" });
app.MapControllerRoute(
    name: "platform-company-provisioned",
    pattern: "platform/companies/{organizationId:guid}/provisioned",
    defaults: new { controller = "Platform", action = "Provisioned" });
app.MapControllerRoute(
    name: "platform-company-edit",
    pattern: "platform/companies/{organizationId:guid}/edit",
    defaults: new { controller = "Platform", action = "EditTenant" });
app.MapControllerRoute(
    name: "platform-company-detail",
    pattern: "platform/companies/{organizationId:guid}",
    defaults: new { controller = "Platform", action = "Tenant" });
app.MapControllerRoute(
    name: "company-login",
    pattern: "company/login",
    defaults: new { controller = "Account", action = "Login" });
app.MapControllerRoute(
    name: "company-logout",
    pattern: "company/logout",
    defaults: new { controller = "Account", action = "Logout" });
app.MapControllerRoute(
    name: "company-forgot-password",
    pattern: "company/forgot-password",
    defaults: new { controller = "Account", action = "ForgotPassword" });
app.MapControllerRoute(
    name: "company-reset-password",
    pattern: "company/reset-password",
    defaults: new { controller = "Account", action = "ResetPassword" });
app.MapControllerRoute(
    name: "company-activate",
    pattern: "company/activate",
    defaults: new { controller = "Account", action = "Activate" });
app.MapControllerRoute(
    name: "company-dashboard",
    pattern: "company",
    defaults: new { controller = "Business", action = "Dashboard" });
app.MapControllerRoute(
    name: "company-inventory",
    pattern: "company/inventory/{action=Index}",
    defaults: new { controller = "Inventory" });
app.MapControllerRoute(
    name: "company-financial-services-module",
    pattern: "company/financial-services/modules/{moduleKey}",
    defaults: new { controller = "FinancialServices", action = "Module" });
app.MapControllerRoute(
    name: "company-payments-finance-module",
    pattern: "company/payments-finance/modules/{moduleKey}",
    defaults: new { controller = "FinancialServices", action = "Module" });
app.MapControllerRoute(
    name: "company-payments-finance",
    pattern: "company/payments-finance/{action=Index}",
    defaults: new { controller = "FinancialServices" });
app.MapControllerRoute(
    name: "company-financial-services",
    pattern: "company/financial-services/{action=Index}",
    defaults: new { controller = "FinancialServices" });
app.MapControllerRoute(
    name: "company-payments",
    pattern: "company/payments/{action=Index}",
    defaults: new { controller = "Payments" });
app.MapControllerRoute(
    name: "company-supplier-search",
    pattern: "company/suppliers/catalog/item-search",
    defaults: new { controller = "Business", action = "SupplierItemSearch" });
app.MapControllerRoute(
    name: "company-supplier-search-results",
    pattern: "company/suppliers/catalog/item-search/results",
    defaults: new { controller = "Business", action = "SupplierItemSearchResults" });
app.MapControllerRoute(
    name: "company-supplier-wps",
    pattern: "company/suppliers/wps",
    defaults: new { controller = "Business", action = "SupplierWpsConnector" });
app.MapControllerRoute(
    name: "company-supplier-turn14",
    pattern: "company/suppliers/turn14",
    defaults: new { controller = "Business", action = "SupplierTurn14Connector" });
app.MapControllerRoute(
    name: "company-supplier-parts-unlimited",
    pattern: "company/suppliers/parts-unlimited",
    defaults: new { controller = "Business", action = "SupplierPartsUnlimitedConnector" });
app.MapControllerRoute(
    name: "company-supplier-wps-inventory",
    pattern: "company/suppliers/wps/inventory",
    defaults: new { controller = "Business", action = "SupplierWpsInventory" });
app.MapControllerRoute(
    name: "company-supplier-turn14-inventory",
    pattern: "company/suppliers/turn14/inventory",
    defaults: new { controller = "Business", action = "SupplierTurn14Inventory" });
app.MapControllerRoute(
    name: "company-supplier-parts-unlimited-inventory",
    pattern: "company/suppliers/parts-unlimited/inventory",
    defaults: new { controller = "Business", action = "SupplierPartsUnlimitedInventory" });
app.MapControllerRoute(
    name: "company-supplier-fitment",
    pattern: "company/suppliers/catalog/fitment",
    defaults: new { controller = "Business", action = "SupplierFetchItemFitment" });
app.MapControllerRoute(
    name: "company-admin",
    pattern: "company/admin/{action=Administration}",
    defaults: new { controller = "Business" });
app.MapControllerRoute(
    name: "company-users-legacy",
    pattern: "company/users/{action=Index}",
    defaults: new { controller = "Employees" });
app.MapControllerRoute(
    name: "company-employees",
    pattern: "company/employees/{action=Index}",
    defaults: new { controller = "Employees" });
app.MapControllerRoute(
    name: "company-customer-detail",
    pattern: "company/customers/{customerId:guid}",
    defaults: new { controller = "Customers", action = "Detail" });
app.MapControllerRoute(
    name: "company-customers",
    pattern: "company/customers/{action=Index}",
    defaults: new { controller = "Customers" });
app.MapControllerRoute(
    name: "company-work-orders",
    pattern: "company/work-orders",
    defaults: new { controller = "WorkOrders", action = "Index" });
app.MapControllerRoute(
    name: "company-service-intake",
    pattern: "company/service/intake",
    defaults: new { controller = "WorkOrders", action = "Intake" });
app.MapControllerRoute(
    name: "company-service-intake-pin",
    pattern: "company/service/intake/pin",
    defaults: new { controller = "WorkOrders", action = "VerifyIntakePin" });
app.MapControllerRoute(
    name: "company-service-intake-clear-pin",
    pattern: "company/service/intake/clear-pin",
    defaults: new { controller = "WorkOrders", action = "ClearIntakePin" });
app.MapControllerRoute(
    name: "company-service-intake-create",
    pattern: "company/service/intake/create",
    defaults: new { controller = "WorkOrders", action = "CreateIntake" });
app.MapControllerRoute(
    name: "company-service-intake-customer-lookup",
    pattern: "company/service/intake/customer-lookup",
    defaults: new { controller = "WorkOrders", action = "CustomerLookup" });
app.MapControllerRoute(
    name: "company-service-intake-ymm-types",
    pattern: "company/service/intake/ymm/types",
    defaults: new { controller = "WorkOrders", action = "YmmTypes" });
app.MapControllerRoute(
    name: "company-service-intake-ymm-years",
    pattern: "company/service/intake/ymm/years",
    defaults: new { controller = "WorkOrders", action = "YmmYears" });
app.MapControllerRoute(
    name: "company-service-intake-ymm-makes",
    pattern: "company/service/intake/ymm/makes",
    defaults: new { controller = "WorkOrders", action = "YmmMakes" });
app.MapControllerRoute(
    name: "company-service-intake-ymm-models",
    pattern: "company/service/intake/ymm/models",
    defaults: new { controller = "WorkOrders", action = "YmmModels" });
app.MapControllerRoute(
    name: "company-work-order-new",
    pattern: "company/work-orders/new",
    defaults: new { controller = "WorkOrders", action = "New" });
app.MapControllerRoute(
    name: "company-work-order-edit",
    pattern: "company/work-orders/{workOrderId:guid}/edit",
    defaults: new { controller = "WorkOrders", action = "Edit" });
app.MapControllerRoute(
    name: "company-work-order-item-lookup",
    pattern: "company/work-orders/item-lookup",
    defaults: new { controller = "WorkOrders", action = "ItemLookup" });
app.MapControllerRoute(
    name: "company-work-order-item-lookup-facets",
    pattern: "company/work-orders/item-lookup/facets",
    defaults: new { controller = "WorkOrders", action = "ItemLookupFacets" });
app.MapControllerRoute(
    name: "company-work-order-fitment-count",
    pattern: "company/work-orders/fitment-count",
    defaults: new { controller = "WorkOrders", action = "FitmentItemCount" });
app.MapControllerRoute(
    name: "company-work-order-wps-inventory",
    pattern: "company/work-orders/wps-inventory",
    defaults: new { controller = "WorkOrders", action = "WpsInventory" });
app.MapControllerRoute(
    name: "company-work-order-turn14-inventory",
    pattern: "company/work-orders/turn14-inventory",
    defaults: new { controller = "WorkOrders", action = "Turn14Inventory" });
app.MapControllerRoute(
    name: "company-work-order-parts-unlimited-inventory",
    pattern: "company/work-orders/parts-unlimited-inventory",
    defaults: new { controller = "WorkOrders", action = "PartsUnlimitedInventory" });
app.MapControllerRoute(
    name: "company-work-order-save",
    pattern: "company/work-orders/save",
    defaults: new { controller = "WorkOrders", action = "Save" });
app.MapControllerRoute(
    name: "company-profile",
    pattern: "company/profile/{action=Index}",
    defaults: new { controller = "Profile" });
app.MapControllerRoute(
    name: "company-setup",
    pattern: "company/setup",
    defaults: new { controller = "Onboarding", action = "Index" });
app.MapControllerRoute(
    name: "admin-marketing",
    pattern: "admin/marketing/{action=Index}/{id?}",
    defaults: new { controller = "MarketingAdmin" });
app.MapControllerRoute(
    name: "admin-login",
    pattern: "admin/login",
    defaults: new { controller = "Platform", action = "Login" });
app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{action=Dashboard}/{id?}",
    defaults: new { controller = "Platform" });
app.MapControllerRoute(
    name: "marketing-page",
    pattern: "{slug:regex(^(?!admin$|platform$|company$|health$|account$|business$|home$|marketingadmin$).+)}",
    defaults: new { controller = "Home", action = "Page" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (app.Configuration.GetValue("Database:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IApplicationDbInitializer>().InitializeAsync();
}

app.Run();
