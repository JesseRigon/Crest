using Crest.Workflows.Studio.Models;

namespace Crest.Workflows.Studio.Contracts;

/// Provides menu items to the dashboard.
public interface IMenuProvider
{
    /// Returns a list of menu items.
    ValueTask<IEnumerable<MenuItem>> GetMenuItemsAsync(CancellationToken cancellationToken = default);
}