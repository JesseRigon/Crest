using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Environment.Shell.Configuration;
using Crest.Shells.Database.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class DatabaseShellsPlatformBuilderExtensions
{
    /// <summary>
    /// Host services to load shells settings and configuration from database.
    /// </summary>
    public static PlatformBuilder AddDatabaseShellsConfiguration(this PlatformBuilder builder)
    {
        var services = builder.ApplicationServices;

        services.Replace(ServiceDescriptor.Singleton<IShellsSettingsSources, DatabaseShellsSettingsSources>());
        services.Replace(ServiceDescriptor.Singleton<IShellConfigurationSources, DatabaseShellConfigurationSources>());

        return builder;
    }
}
