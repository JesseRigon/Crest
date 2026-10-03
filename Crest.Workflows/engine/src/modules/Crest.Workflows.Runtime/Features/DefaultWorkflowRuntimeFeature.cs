using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;

namespace Crest.Workflows.Runtime.Features;

/// <summary>
/// Installs the default runtime services.
/// </summary>
[DependsOn(typeof(WorkflowRuntimeFeature))]
public class DefaultWorkflowRuntimeFeature(IModule module) : FeatureBase(module);