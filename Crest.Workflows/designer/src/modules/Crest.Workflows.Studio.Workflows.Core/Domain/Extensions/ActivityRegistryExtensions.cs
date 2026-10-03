using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IActivityRegistry"/> interface.
/// </summary>
public static class ActivityRegistryExtensions
{
    /// <summary>
    /// Returns a list of activity descriptors that are browsable.
    /// </summary>
    public static IEnumerable<ActivityDescriptor> ListBrowsable(this IActivityRegistry activityRegistry) => 
        activityRegistry.List().Where(x => x.IsBrowsable).ToList();
}