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
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Platform/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            var loginPath = context.Request.Path.StartsWithSegments("/Platform")
                ? "/Platform/Login"
                : "/Account/Login";
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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Platform}/{action=Dashboard}/{id?}");

if (app.Configuration.GetValue("Database:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IApplicationDbInitializer>().InitializeAsync();
}

app.Run();
