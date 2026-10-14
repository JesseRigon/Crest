using Crest.OpenApi.Drivers;
using Crest.Routing;

namespace Crest.OpenApi;

/// <summary>
/// The OpenAPI settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("openapi", "OpenApi", "settings", OpenApiSettingsDisplayDriver.GroupId, [OpenApiPermissions.ManageOpenApi.Name]);
    }
}
