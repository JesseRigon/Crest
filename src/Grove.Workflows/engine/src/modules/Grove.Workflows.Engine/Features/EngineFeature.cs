using Grove.Workflows.Common.Features;
using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Activities;
using Grove.Workflows.Features;
using Grove.Workflows.Management.Features;
using Grove.Workflows.Runtime.Features;

namespace Grove.Workflows.Features;

/// <summary>
/// Represents Grove.Workflows as a feature of the system.
/// </summary>
[DependsOn(typeof(MediatorFeature))]
[DependsOn(typeof(WorkflowsFeature))]
[DependsOn(typeof(FlowchartFeature))]
[DependsOn(typeof(DefaultWorkflowRuntimeFeature))]
[DependsOn(typeof(WorkflowManagementFeature))]
public class EngineFeature : FeatureBase
{
    /// <summary>
    /// Set this to true to opt out of automatically registering activities from Grove.Workflows.Core.
    /// </summary>
    public bool DisableAutomaticActivityRegistration { get; set; }

    /// <inheritdoc />
    public EngineFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Module
            .UseWorkflows(workflows => workflows
                .WithDefaultWorkflowExecutionPipeline()
                .WithDefaultActivityExecutionPipeline())
            .UseWorkflowManagement(management =>
            {
                if (!DisableAutomaticActivityRegistration)
                    management
                        .AddActivitiesFrom<WriteLine>()
                        .RemoveActivity<ReadLine>() // ReadLine is not commonly used and can cause "hanging" containers when awaiting user input. Better to opt-in explicitly. 
                        ;
            });
    }
}