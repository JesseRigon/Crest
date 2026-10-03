using Crest.Workflows;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddStorageDriver<T>() where T : class, IStorageDriver
        {
            return services.AddScoped<IStorageDriver, T>();
        }

        public IServiceCollection AddActivityStateFilter<T>() where T : class, IActivityStateFilter
        {
            return services.AddScoped<IActivityStateFilter, T>();
        }
    }
}