using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Activities.Flowchart.Models;
using Crest.Workflows.Activities.Flowchart.Options;
using Crest.Workflows.Activities.Flowchart.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Features;

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