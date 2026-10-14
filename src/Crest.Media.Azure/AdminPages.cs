using Crest.Routing;

namespace Crest.Media.Azure;

/// <summary>
/// The Azure Blob media options: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("azure-blob-options", "Azure Blob Options", "settings-media", "Crest.Media.Azure", "Admin", "Options", [Permissions.ViewAzureMediaOptions.Name]);
    }
}
