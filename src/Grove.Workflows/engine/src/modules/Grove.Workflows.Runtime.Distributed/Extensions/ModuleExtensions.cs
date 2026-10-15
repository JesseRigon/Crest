using Grove.Workflows.Runtime.Distributed.Features;
using Grove.Workflows.Runtime.Features;

namespace Grove.Workflows.Runtime.Distributed.Extensions;

public static class ModuleExtensions
{
    public static WorkflowRuntimeFeature UseDistributedRuntime(this WorkflowRuntimeFeature feature, Action<DistributedRuntimeFeature>? configure = null)
    {
        feature.Module.Configure(configure);
        return feature;
    }
}