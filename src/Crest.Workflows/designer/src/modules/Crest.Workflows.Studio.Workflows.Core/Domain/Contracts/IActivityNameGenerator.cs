using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// Generates unique activity names.
/// </summary>
public interface IActivityNameGenerator
{
    /// <summary>
    /// Returns true if the specified name already exists in the container.
    /// </summary>
    bool GetNameExists(IEnumerable<JsonObject> activities, string name);
    
    /// <summary>
    /// Generates a unique name for the specified activity descriptor.
    /// </summary>
    string GenerateNextName(IEnumerable<JsonObject> activities, ActivityDescriptor activityDescriptor);
}