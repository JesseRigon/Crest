using Microsoft.Extensions.DependencyInjection;

namespace Crest.Icons;

public static class IconClientServiceCollectionExtensions
{
    public static IServiceCollection AddIconClient(this IServiceCollection services)
    {
        services.AddScoped<IIconSearchClient, IconSearchClient>();
        services.AddSingleton<ClientIconRegistry>();

        return services;
    }
}
