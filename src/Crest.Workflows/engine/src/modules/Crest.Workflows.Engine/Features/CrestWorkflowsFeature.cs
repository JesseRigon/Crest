using Crest.Workflows.Common.Features;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Activities;
using Crest.Workflows.Features;
using Crest.Workflows.Management.Features;
using Crest.Workflows.Runtime.Features;

namespace Crest.Workflows.Features;

/// <summary>
/// Represents Crest.Workflows as a feature of the system.
/// </summary>
[DependsOn(typeof(MediatorFeature))]
[DependsOn(typeof(WorkflowsFeature))]
[DependsOn(typeof(FlowchartFeature))]
[DependsOn(typeof(DefaultWorkflowRuntimeFeature))]
[DependsOn(typeof(WorkflowManagementFeature))]
public class CrestWorkflowsFeature : FeatureBase
{
    /// <summary>
    /// Set this to true to opt out of automatically registering activities from Crest.Workflows.Core.
    /// </summary>
    public bool DisableAutomaticActivityRegistration { get; set; }

    /// <inheritdoc />
    public CrestWorkflowsFeature(IModule module) : base(module)
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