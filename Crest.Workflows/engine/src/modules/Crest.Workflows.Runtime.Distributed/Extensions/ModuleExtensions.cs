using Crest.Workflows.Runtime.Distributed.Features;
using Crest.Workflows.Runtime.Features;

namespace Crest.Workflows.Runtime.Distributed.Extensions;

public static class ModuleExtensions
{
    public static WorkflowRuntimeFeature UseDistributedRuntime(this WorkflowRuntimeFeature feature, Action<DistributedRuntimeFeature>? configure = null)
    {
        feature.Module.Configure(configure);
        return feature;
    }
}