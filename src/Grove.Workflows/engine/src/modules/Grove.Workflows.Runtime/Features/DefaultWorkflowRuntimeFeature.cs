using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;

namespace Grove.Workflows.Runtime.Features;

/// <summary>
/// Installs the default runtime services.
/// </summary>
[DependsOn(typeof(WorkflowRuntimeFeature))]
public class DefaultWorkflowRuntimeFeature(IModule module) : FeatureBase(module);