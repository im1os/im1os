using iM1os.Application.Common;
using iM1os.Infrastructure;
using iM1os.Infrastructure.Services;
using iM1os.Workers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, NoCurrentUser>();
builder.Services.AddHostedService<PlatformMaintenanceWorker>();

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

var host = builder.Build();
host.Run();
