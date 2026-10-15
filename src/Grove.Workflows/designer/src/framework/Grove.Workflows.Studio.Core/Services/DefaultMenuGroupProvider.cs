using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Models;

namespace Grove.Workflows.Studio.Services;

/// <summary>
/// Provides default menu group services.
/// </summary>
public class DefaultMenuGroupProvider : IMenuGroupProvider
{
    /// <summary>
    /// Provides the get menu groups async.
    /// </summary>
    public ValueTask<IEnumerable<MenuItemGroup>> GetMenuGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = new List<MenuItemGroup>
        {
            MenuItemGroups.General,
            MenuItemGroups.Settings
        };

        return new(groups);
    }
}