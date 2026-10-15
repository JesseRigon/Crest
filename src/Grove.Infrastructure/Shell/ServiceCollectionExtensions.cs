using System.IO.Enumeration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Builders;
using Grove.Environment.Shell.Descriptor;
using Grove.Environment.Shell.Descriptor.Settings;
using Grove.Environment.Shell.Distributed;
using Grove.Environment.Shell.Models;
using Grove.Environment.Shell.Removing;

namespace Grove.Environment.Shell;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostingShellServices(this IServiceCollection services)
    {
        services.AddSingleton<IShellHost, ShellHost>();
        services.AddSingleton<IShellDescriptorManagerEventHandler>(sp => sp.GetRequiredService<IShellHost>());

        {
            // Use a single default site by default, i.e. if WithTenants hasn't been called before
            services.TryAddSingleton<IShellSettingsManager, SingleShellSettingsManager>();
            services.AddTransient<IConfigureOptions<ShellOptions>, ShellOptionsSetup>();

            services.AddSingleton<IShellContextFactory, ShellContextFactory>();
            {
                services.AddSingleton<ICompositionStrategy, CompositionStrategy>();

                services.AddSingleton<IShellContainerFactory, ShellContainerFactory>();
            }
        }

        services.AddSingleton<IRunningShellTable, RunningShellTable>();

        services.AddHostedService<DistributedShellHostedService>();

        services.AddSingleton<IShellRemovalManager, ShellRemovalManager>();
        services.AddSingleton<IShellRemovingHandler, ShellWebRootRemovingHandler>();
        services.AddSingleton<IShellRemovingHandler, ShellSiteFolderRemovingHandler>();
        services.AddSingleton<IShellRemovingHandler, ShellSettingsRemovingHandler>();

        return services;
    }

    public static IServiceCollection AddAllFeaturesDescriptor(this IServiceCollection services)
    {
        services.AddScoped<IShellDescriptorManager, AllFeaturesShellDescriptorManager>();

        return services;
    }

    public static IServiceCollection AddSetFeaturesDescriptor(this IServiceCollection services)
    {
        services.AddSingleton<IShellDescriptorManager, SetFeaturesShellDescriptorManager>();

        return services;
    }

    public static IServiceCollection AddNullFeatureProfilesService(this IServiceCollection services)
        => services.AddScoped<IFeatureProfilesService, NullFeatureProfilesService>();

    public static IServiceCollection AddFeatureValidation(this IServiceCollection services)
        => services
            .AddScoped<IFeatureValidationProvider, FeatureProfilesValidationProvider>()
            .AddScoped<IFeatureValidationProvider, DefaultTenantOnlyFeatureValidationProvider>()
            .AddScoped<IFeatureValidationProvider, ApplicationFeatureValidationProvider>();

    public static IServiceCollection ConfigureFeatureProfilesRuleOptions(this IServiceCollection services)
        => services
            .Configure<FeatureProfilesRuleOptions>(o =>
            {
                o.Rules["Include"] = (expression, name) =>
                {
                    if (FileSystemName.MatchesSimpleExpression(expression, name))
                    {
                        return (true, true);
                    }

                    return (false, false);
                };

                o.Rules["Exclude"] = (expression, name) =>
                {
                    if (FileSystemName.MatchesSimpleExpression(expression, name))
                    {
                        return (true, false);
                    }

                    return (false, false);
                };
            });
}
