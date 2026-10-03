using System.Collections.Concurrent;
using Crest.Workflows.Features;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Extensions;

/// <summary>
/// Provides extension methods to <see cref="IServiceCollection"/>. 
/// </summary>
public static class ModuleExtensions
{
    private static readonly IDictionary<IServiceCollection, IModule> Modules = new ConcurrentDictionary<IServiceCollection, IModule>();
    
    /// <summary>
    /// Creates a new Crest.Workflows module and adds the <see cref="CrestWorkflowsFeature"/> to it.
    /// </summary>
    public static IModule AddCrestWorkflows(this IServiceCollection services, Action<IModule>? configure = null)
    {
        var module = services.GetOrCreateModule();
        module.Configure<AppFeature>(app => app.Configurator += configure);
        module.Apply();
        
        return module;
    }
    
    /// <summary>
    /// Configures the Crest.Workflows module.
    /// </summary>
    public static IModule ConfigureCrestWorkflows(this IServiceCollection services, Action<IModule>? configure = null)
    {
        var module = services.GetOrCreateModule();
        
        if(configure != null)
            module.Configure<AppFeature>(app => app.Configurator += configure);
        
        return module;
    }
    
    private static IModule GetOrCreateModule(this IServiceCollection services)
    {
        if(Modules.TryGetValue(services, out var module))
            return module;
        
        module = services.CreateModule();
        
        Modules[services] = module;
        return module;
    }
}