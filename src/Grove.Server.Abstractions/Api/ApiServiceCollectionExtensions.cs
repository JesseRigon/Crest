using Grove.Api;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Registers a module's API contributor in the tenant (call from the owning feature's startup).</summary>
    public static IServiceCollection AddApiContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IApiContributor
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApiContributor, TContributor>());
        return services;
    }

    /// <summary>Registers a source of defined API entries in the tenant.</summary>
    public static IServiceCollection AddDefinedApiSource<TSource>(this IServiceCollection services)
        where TSource : class, IDefinedApiSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDefinedApiSource, TSource>());
        return services;
    }

    /// <summary>Registers an exception mapper of the error contract.</summary>
    public static IServiceCollection AddApiExceptionMapper<TMapper>(this IServiceCollection services)
        where TMapper : class, IApiExceptionMapper
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApiExceptionMapper, TMapper>());
        return services;
    }
}
