using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Activities.Flowchart.Models;
using Grove.Workflows.Activities.Flowchart.Options;
using Grove.Workflows.Activities.Flowchart.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Features;

/// <summary>
/// Adds support for the Flowchart activity.
/// </summary>
public class FlowchartFeature : FeatureBase
{
    /// <inheritdoc />
    public FlowchartFeature(IModule module) : base(module)
    {
    }

    /// <summary>
    /// A delegate to configure <see cref="FlowchartOptions"/>.
    /// </summary>
    public Action<FlowchartOptions>? FlowchartOptionsConfigurator { get; set; }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddSerializationOptionsConfigurator<FlowchartSerializationOptionConfigurator>();

        // Register FlowchartOptions
        Services.AddOptions<FlowchartOptions>();

        if (FlowchartOptionsConfigurator != null)
            Services.Configure(FlowchartOptionsConfigurator);
    }

    public override void Configure()
    {
        Module.AddTypeAlias<FlowScope>("FlowScope");
    }
}