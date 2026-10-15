using Microsoft.Extensions.DependencyInjection;

namespace Grove.Icons;

public static class IconClientServiceCollectionExtensions
{
    public static IServiceCollection AddIconClient(this IServiceCollection services)
    {
        services.AddScoped<IIconSearchClient, IconSearchClient>();
        services.AddSingleton<ClientIconRegistry>();

        return services;
    }
}
