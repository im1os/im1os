using iM1os.Application.Common;
using iM1os.Application.GlobalCatalog;
using iM1os.Infrastructure;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using iM1os.Workers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, NoCurrentUser>();
builder.Services.AddHostedService<PlatformMaintenanceWorker>();
builder.Services.AddHostedService<WpsMasterItemListImportWorker>();

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

var host = builder.Build();

if (args.Contains("--run-wps-master-import", StringComparer.OrdinalIgnoreCase))
{
    using var scope = host.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue("Database:AutoMigrate", false))
    {
        await scope.ServiceProvider.GetRequiredService<IApplicationDbInitializer>().InitializeAsync();
    }

    var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
    var importService = scope.ServiceProvider.GetRequiredService<IWpsMasterItemListImportService>();
    var maxItems = ReadIntArg(args, "--max-items");
    var importRunId = await WpsMasterItemListImportService.EnsureWpsImportRunAsync(dbContext, clock, maxItems, CancellationToken.None);
    var result = await importService.ImportAsync(new WpsMasterItemListImportRequest(importRunId, maxItems), CancellationToken.None);

    Console.WriteLine($"WPS Master Item List import complete. Run={result.ImportRunId}, Processed={result.Processed}, CreatedGlobalProducts={result.CreatedGlobalProducts}, UpdatedGlobalProducts={result.UpdatedGlobalProducts}, CreatedSupplierProducts={result.CreatedSupplierProducts}, UpdatedSupplierProducts={result.UpdatedSupplierProducts}, UpsertedPrices={result.UpsertedPrices}.");
    return;
}

if (args.Contains("--run-indie-fitment-import", StringComparer.OrdinalIgnoreCase))
{
    using var scope = host.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue("Database:AutoMigrate", false))
    {
        await scope.ServiceProvider.GetRequiredService<IApplicationDbInitializer>().InitializeAsync();
    }

    var importService = scope.ServiceProvider.GetRequiredService<IIndieMotoFitmentImportService>();
    var supplierCode = ReadStringArg(args, "--supplier") ?? "WPS";
    var sku = ReadStringArg(args, "--sku");
    var maxSkus = ReadIntArg(args, "--max-skus");
    var fitmentLimit = ReadIntArg(args, "--fitment-limit");
    var delayMilliseconds = ReadIntArg(args, "--delay-ms") ?? 250;
    var baseUrl = ReadStringArg(args, "--base-url") ?? "https://saas.indie-moto.com";
    var result = await importService.ImportAsync(
        new IndieMotoFitmentImportRequest(supplierCode, sku, maxSkus, fitmentLimit, delayMilliseconds, baseUrl),
        CancellationToken.None);

    Console.WriteLine($"Indie Moto fitment import complete. Supplier={supplierCode}, Sku={sku ?? "*"}, FitmentLimit={fitmentLimit?.ToString() ?? "*"}, SkusProcessed={result.SkusProcessed}, SkusWithFitment={result.SkusWithFitment}, SkusQueuedForPartsUnlimitedCrawl={result.SkusQueuedForPartsUnlimitedCrawl}, SkusWithoutFitment={result.SkusWithoutFitment}, FitmentRowsProcessed={result.FitmentRowsProcessed}, SourceFitmentRowsUpserted={result.SourceFitmentRowsUpserted}, GlobalVehiclesUpserted={result.GlobalVehiclesUpserted}, VehicleFitmentsUpserted={result.VehicleFitmentsUpserted}, FailedSkus={result.FailedSkus}, DelayMs={delayMilliseconds}.");
    return;
}

host.Run();

static string? ReadStringArg(string[] args, string name)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[index + 1];
        }
    }

    return null;
}

static int? ReadIntArg(string[] args, string name)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[index + 1], out var parsed))
        {
            return parsed;
        }
    }

    return null;
}
