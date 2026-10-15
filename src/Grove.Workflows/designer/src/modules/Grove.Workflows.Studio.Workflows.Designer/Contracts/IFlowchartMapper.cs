using System.Text.Json.Nodes;
using Grove.Workflows.Studio.Workflows.Designer.Models;
using Grove.Workflows.Studio.Workflows.Domain.Models;
using Grove.Workflows.Studio.Workflows.UI.Models;

namespace Grove.Workflows.Studio.Workflows.Designer.Contracts;

/// <summary>
/// Maps a Flowchart activity from and to an X6Graph.
/// </summary>
public interface IFlowchartMapper
{
    /// <summary>
    /// Maps a flowchart activity to an X6Graph.
    /// </summary>
    /// <param name="flowchart">The flowchart activity.</param>
    /// <param name="activityStatsMap">A map of activity stats.</param>
    X6Graph Map(JsonObject flowchart, IDictionary<string, ActivityStats>? activityStatsMap = null);
    
    /// <summary>
    /// Maps an X6 graph to a flowchart activity.
    /// </summary>
    /// <param name="graph">The X6 graph.</param>
    JsonObject Map(X6Graph graph);
    
}