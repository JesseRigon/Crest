using Grove.Workflows.Common;
using Grove.Workflows.Common.RecurringTasks;
using Grove.Workflows.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Adds a serialization options configurator for the specified type.
    /// </summary>
    public static IServiceCollection AddSerializationOptionsConfigurator<T>(this IServiceCollection services) where T : class, ISerializationOptionsConfigurator
    {
        services.AddSingleton<ISerializationOptionsConfigurator, T>();
        return services;
    }
    
    public static IServiceCollection AddStartupTask<T>(this IServiceCollection services) where T : class, IStartupTask
    {
        return services.AddScoped<IStartupTask, T>();
    }
    
    public static IServiceCollection AddBackgroundTask<T>(this IServiceCollection services) where T : class, IBackgroundTask
    {
        return services.AddScoped<IBackgroundTask, T>();
    }
    
    public static IServiceCollection AddRecurringTask<T>(this IServiceCollection services) where T : class, IRecurringTask
    {
        return services.AddScoped<IRecurringTask, T>();
    }
    
    public static IServiceCollection AddRecurringTask<T>(this IServiceCollection services, TimeSpan interval) where T : class, IRecurringTask
    {
        services.Configure<RecurringTaskOptions>(options => options.Schedule.ConfigureTask<T>(interval));
        return services.AddRecurringTask<T>();
    }
}