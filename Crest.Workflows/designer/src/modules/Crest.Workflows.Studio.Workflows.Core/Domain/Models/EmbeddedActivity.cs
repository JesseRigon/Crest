using System.Text.Json.Nodes;

namespace Crest.Workflows.Studio.Workflows.Domain.Models;

/// <summary>
/// Represents an embedded activity.
/// </summary>
/// <param name="Activity">The embedded activity.</param>
/// <param name="PropertyName">The name of the property that contains the embedded activity.</param>
public record EmbeddedActivity(JsonObject Activity, string PropertyName);