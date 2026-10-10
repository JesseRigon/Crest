using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Recipes.Services;

namespace Crest.Recipes;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRecipeExecutionStep<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IRecipeStepHandler
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecipeStepHandler, TImplementation>());

        return services;
    }
}
