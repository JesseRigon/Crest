using Microsoft.Extensions.Options;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Data.Descriptors;
using Grove.Environment.Shell.Descriptor;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShellPlatformBuilderExtensions
{
    /// <summary>
    /// Adds services at the host level to load site settings from the file system
    /// and tenant level services to store states and descriptors in the database.
    /// </summary>
    public static PlatformBuilder AddDataStorage(this PlatformBuilder builder)
    {
        builder.AddSitesFolder()
            .ConfigureServices(services =>
            {
                services.AddScoped<IShellDescriptorManager, ShellDescriptorManager>();
            });

        return builder;
    }

    /// <summary>
    /// Host services to load site settings from the file system.
    /// </summary>
    public static PlatformBuilder AddSitesFolder(this PlatformBuilder builder)
    {
        var services = builder.ApplicationServices;

        services.AddSingleton<IShellsSettingsSources, ShellsSettingsSources>();
        services.AddSingleton<IShellsConfigurationSources, ShellsConfigurationSources>();
        services.AddSingleton<IShellConfigurationSources, ShellConfigurationSources>();
        services.AddTransient<IConfigureOptions<ShellOptions>, ShellOptionsSetup>();
        services.AddSingleton<IShellSettingsManager, ShellSettingsManager>();

        return builder;
    }
}
