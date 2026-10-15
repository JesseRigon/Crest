using System.Text.Json.Nodes;
using Grove.Workflows.Api.Client.Extensions;
using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Localization;
using Grove.Workflows.Studio.Workflows.UI.Contracts;
using JetBrains.Annotations;

namespace Grove.Workflows.Studio.Workflows.DiagramDesigners.Flowcharts;

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
    public bool GetSupportsActivity(JsonObject activity) => activity.GetTypeName() == "Grove.Workflows.Flowchart";

    /// <inheritdoc />
    public IDiagramDesigner GetEditor() => new FlowchartDiagramDesigner(localizer);
}