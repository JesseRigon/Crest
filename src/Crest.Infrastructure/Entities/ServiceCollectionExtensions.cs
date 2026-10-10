using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Entities.Scripting;
using Crest.Scripting;

namespace Crest.Entities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdGeneration(this IServiceCollection services)
    {
        services.TryAddSingleton<IIdGenerator, DefaultIdGenerator>();
        services.AddSingleton<IGlobalMethodProvider, IdGeneratorMethod>();

        return services;
    }
}
