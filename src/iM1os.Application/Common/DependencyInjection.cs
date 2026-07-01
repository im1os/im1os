using Microsoft.Extensions.DependencyInjection;
using iM1os.Application.GlobalCatalog;
using iM1os.Application.Parts;

namespace iM1os.Application.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductMatchingService, ProductMatchingService>();
        services.AddScoped<IPartsEngineService, PartsEngineService>();

        return services;
    }
}
