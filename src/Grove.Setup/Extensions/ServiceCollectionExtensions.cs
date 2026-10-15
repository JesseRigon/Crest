using Microsoft.Extensions.DependencyInjection;
using Grove.Setup.Services;

namespace Grove.Setup;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds tenant level services.
    /// </summary>
    public static IServiceCollection AddSetup(this IServiceCollection services)
    {
        services.AddScoped<ISetupService, SetupService>();

        services.AddSingleton<ISetupUserIdGenerator, SetupUserIdGenerator>();

        return services;
    }
}
