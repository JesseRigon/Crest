using Grove.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Grove.Workflows.Studio.Workflows.UI.Models;

namespace Grove.Workflows.Studio.Workflows.UI.Contracts;

/// <summary>
/// Provides mappings between activity types and icons.
/// </summary>
public interface IActivityDisplaySettingsProvider
{
    /// <summary>
    /// Returns a dictionary of activity type to display settings.
    /// </summary>
    /// <param name="activityDescriptor"></param>
    /// <returns></returns>
    IDictionary<string, ActivityDisplaySettings> GetSettings();
}