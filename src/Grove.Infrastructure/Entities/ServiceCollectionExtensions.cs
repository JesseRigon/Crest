using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Entities.Scripting;
using Grove.Scripting;

namespace Grove.Entities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdGeneration(this IServiceCollection services)
    {
        services.TryAddSingleton<IIdGenerator, DefaultIdGenerator>();
        services.AddSingleton<IGlobalMethodProvider, IdGeneratorMethod>();

        return services;
    }
}
