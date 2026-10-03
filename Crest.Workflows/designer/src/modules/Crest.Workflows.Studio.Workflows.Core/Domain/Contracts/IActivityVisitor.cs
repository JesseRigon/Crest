using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Shared.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// Represents a visitor that can visit an activity.
/// </summary>
public interface IActivityVisitor
{
    /// <summary>
    /// Visits the specified activity and returns a graph of activities.
    /// </summary>
    /// <param name="activity"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ActivityNode> VisitAsync(JsonObject activity, CancellationToken cancellationToken = default);
}