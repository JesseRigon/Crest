using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Resilience.Features;

namespace Grove.Workflows.Resilience.Extensions;

public static class ModuleExtensions
{
    public static IModule UseResilience(this IModule module, Action<ResilienceFeature>? configure = null)
    {
        return module.Use(configure);
    }
}