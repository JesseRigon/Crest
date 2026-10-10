using Microsoft.Extensions.DependencyInjection;

namespace Crest.Queries;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers a query source under its key; a source that is also an <see cref="IQueryDescriber"/> is registered as one.</summary>
    public static IServiceCollection AddQuerySource<TSource>(this IServiceCollection services, string sourceName)
        where TSource : class, IQuerySource
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        services.AddScoped<TSource>();
        services.AddScoped<IQuerySource>(sp => sp.GetService<TSource>());
        services.AddKeyedScoped<IQuerySource>(sourceName, (sp, key) => sp.GetService<TSource>());

        if (typeof(IQueryDescriber).IsAssignableFrom(typeof(TSource)))
        {
            services.AddScoped(sp => (IQueryDescriber)sp.GetService<TSource>());
        }

        return services;
    }
}
