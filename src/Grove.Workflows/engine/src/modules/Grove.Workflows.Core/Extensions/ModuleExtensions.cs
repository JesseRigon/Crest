using Grove.Workflows.Features.Services;
using Grove.Workflows.Features;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Extensions;

public static class ModuleExtensions
{
    extension(IModule configuration)
    {
        public IModule UseWorkflows(Action<WorkflowsFeature>? configure = null)
        {
            configuration.Configure(configure);
            return configuration;
        }

        public IModule UseFlowchart(Action<FlowchartFeature>? configure = null)
        {
            configuration.Configure(configure);
            return configuration;
        }
    }
}