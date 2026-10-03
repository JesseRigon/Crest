using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Resilience.Features;

namespace Crest.Workflows.Resilience.Extensions;

public static class ModuleExtensions
{
    public static IModule UseResilience(this IModule module, Action<ResilienceFeature>? configure = null)
    {
        return module.Use(configure);
    }
}