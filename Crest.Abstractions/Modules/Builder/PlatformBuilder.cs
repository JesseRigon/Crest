using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Environment.Shell.Builders;
using Crest.Environment.Shell.Descriptor.Models;
using Crest.Modules;

namespace Microsoft.Extensions.DependencyInjection;

public class PlatformBuilder
{
    private Dictionary<int, StartupActions> _actions { get; } = [];

    public PlatformBuilder(IServiceCollection services)
    {
        ApplicationServices = services;
    }

    public IServiceCollection ApplicationServices { get; }

    public PlatformBuilder RegisterStartup<T>() where T : class, IStartup
    {
        ApplicationServices.AddTransient<IStartup, T>();
        return this;
    }

    /// <summary>
    /// This method gets called for each tenant. Use this method to add services to the container.
    /// For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940.
    /// </summary>
    /// <param name="configure">The action to execute when configuring the services for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder ConfigureServices(Action<IServiceCollection, IServiceProvider> configure, int order = 0)
    {
        if (!_actions.TryGetValue(order, out var actions))
        {
            actions = _actions[order] = new StartupActions(order);

            ApplicationServices.AddTransient<IStartup>(sp => new StartupActionsStartup(
                sp.GetRequiredService<IServiceProvider>(), actions, order));
        }

        actions.ConfigureServicesActions.Add(configure);

        return this;
    }

    /// <summary>
    /// This method gets called for each tenant. Use this method to add services to the container.
    /// For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940.
    /// </summary>
    /// <param name="configure">The action to execute when configuring the services for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder ConfigureServices(Action<IServiceCollection> configure, int order = 0)
    {
        return ConfigureServices((s, sp) => configure(s), order);
    }

    /// <summary>
    /// This method gets called for each tenant. Use this method to configure the request's pipeline.
    /// </summary>
    /// <param name="configure">The action to execute when configuring the request's pipeline for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder Configure(Action<IApplicationBuilder, IEndpointRouteBuilder, IServiceProvider> configure, int order = 0)
    {
        if (!_actions.TryGetValue(order, out var actions))
        {
            actions = _actions[order] = new StartupActions(order);

            ApplicationServices.AddTransient<IStartup>(sp => new StartupActionsStartup(
                sp.GetRequiredService<IServiceProvider>(), actions, order));
        }

        actions.ConfigureActions.Add(configure);

        return this;
    }

    /// <summary>
    /// This method gets called for each tenant. Use this method to configure the request's pipeline.
    /// </summary>
    /// <param name="configure">The action to execute when configuring the request's pipeline for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder Configure(Action<IApplicationBuilder, IEndpointRouteBuilder> configure, int order = 0)
    {
        return Configure((app, routes, sp) => configure(app, routes), order);
    }

    /// <summary>
    /// This method gets called for each tenant. Use this method to configure the request's pipeline.
    /// </summary>
    /// <param name="configure">The action to execute when configuring the request's pipeline for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder Configure(Action<IApplicationBuilder> configure, int order = 0)
    {
        return Configure((app, routes, sp) => configure(app), order);
    }

    /// <summary>
    /// This async action gets called for each tenant. Use this method to configure the tenant pipeline.
    /// </summary>
    /// <param name="configureAsync">The async action to execute when configuring the request's pipeline for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder Configure(Func<IApplicationBuilder, IEndpointRouteBuilder, IServiceProvider, ValueTask> configureAsync, int order = 0)
    {
        if (!_actions.TryGetValue(order, out var actions))
        {
            actions = _actions[order] = new StartupActions(order);

            ApplicationServices.AddTransient<IStartup>(sp => new StartupActionsStartup(
                sp.GetRequiredService<IServiceProvider>(), actions, order));
        }

        actions.AsyncConfigureActions.Add(configureAsync);

        return this;
    }

    /// <summary>
    /// This async action gets called for each tenant. Use this method to configure the tenant pipeline.
    /// </summary>
    /// <param name="configureAsync">The async action to execute when configuring the request's pipeline for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder Configure(Func<IApplicationBuilder, IEndpointRouteBuilder, ValueTask> configureAsync, int order = 0)
    {
        return Configure((app, routes, sp) => configureAsync(app, routes), order);
    }

    /// <summary>
    /// This async action gets called for each tenant. Use this method to configure the tenant pipeline.
    /// </summary>
    /// <param name="configureAsync">The async action to execute when configuring the request's pipeline for a tenant.</param>
    /// <param name="order">The order of the action to execute. Lower values will be executed first.</param>
    public PlatformBuilder Configure(Func<IApplicationBuilder, ValueTask> configureAsync, int order = 0)
    {
        return Configure((app, routes, sp) => configureAsync(app), order);
    }

    public PlatformBuilder EnableFeature(string id)
    {
        return ConfigureServices(services =>
        {
            for (var index = 0; index < services.Count; index++)
            {
                var service = services[index];
                if (service.GetImplementationInstance() is ShellFeature feature &&
                    string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            services.AddSingleton(new ShellFeature(id, alwaysEnabled: true));
        });
    }
}
