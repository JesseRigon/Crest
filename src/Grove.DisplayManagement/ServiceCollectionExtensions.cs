using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Settings;

namespace Grove.DisplayManagement.Handlers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDisplayDriver<TModel, TDriver>(this IServiceCollection services)
        where TDriver : class, IDisplayDriver<TModel>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDisplayDriver<TModel>, TDriver>());

        return services;
    }
}
