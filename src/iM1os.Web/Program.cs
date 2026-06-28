using iM1os.Application.Common;
using iM1os.Infrastructure;
using iM1os.Infrastructure.Persistence;
using iM1os.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
