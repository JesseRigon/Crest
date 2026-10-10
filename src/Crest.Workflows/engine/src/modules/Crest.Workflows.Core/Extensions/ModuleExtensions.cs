using Crest.Workflows.Features.Services;
using Crest.Workflows.Features;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

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