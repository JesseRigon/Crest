using System.Collections.Concurrent;
using Grove.Workflows.Features;
using Grove.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Extensions;

/// <summary>
/// Provides extension methods to <see cref="IServiceCollection"/>. 
/// </summary>
public static class ModuleExtensions
{
    private static readonly IDictionary<IServiceCollection, IModule> Modules = new ConcurrentDictionary<IServiceCollection, IModule>();
    
    /// <summary>
    /// Creates a new Grove.Workflows module and adds the <see cref="EngineFeature"/> to it.
    /// </summary>
    public static IModule AddGroveWorkflows(this IServiceCollection services, Action<IModule>? configure = null)
    {
        var module = services.GetOrCreateModule();
        module.Configure<AppFeature>(app => app.Configurator += configure);
        module.Apply();
        
        return module;
    }
    
    /// <summary>
    /// Configures the Grove.Workflows module.
    /// </summary>
    public static IModule ConfigureGroveWorkflows(this IServiceCollection services, Action<IModule>? configure = null)
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