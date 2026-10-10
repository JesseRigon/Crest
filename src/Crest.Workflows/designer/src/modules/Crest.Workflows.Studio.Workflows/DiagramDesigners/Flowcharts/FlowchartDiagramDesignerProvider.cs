using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Extensions;
using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Localization;
using Crest.Workflows.Studio.Workflows.UI.Contracts;
using JetBrains.Annotations;

namespace Crest.Workflows.Studio.Workflows.DiagramDesigners.Flowcharts;

/// <summary>
/// A diagram designer provider for the Flowchart designer.
/// </summary>
[UsedImplicitly]
/// <summary>
/// Provides flowchart diagram designer services.
/// </summary>
public class FlowchartDiagramDesignerProvider(ILocalizer localizer) : IDiagramDesignerProvider
{
    /// <inheritdoc />
    public double Priority => 0;

    /// <inheritdoc />
    public bool GetSupportsActivity(JsonObject activity) => activity.GetTypeName() == "Crest.Workflows.Flowchart";

    /// <inheritdoc />
    public IDiagramDesigner GetEditor() => new FlowchartDiagramDesigner(localizer);
}