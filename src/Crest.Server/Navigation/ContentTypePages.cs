using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.Contents;
using Crest.Contents.Security;
using Crest.Routing;
using Crest.Settings;
using Crest.ContentTypes;

namespace Crest.Navigation;

/// <summary>
/// One Content-menu entry per content type designated with
/// <see cref="CrestContentTypeMenuSettings.ShowInContentMenu"/> (dynamic page registry entries),
/// linking to the generic content-items page filtered to that type. The type filter travels as a
/// query string, so the <c>/Contents/ContentItems</c> page's own declaration gates the view.
/// </summary>
public sealed class ContentTypePages(IContentDefinitionManager contentDefinitionManager) : IDynamicPageSource
{
    internal static bool ShowsInContentMenu(ContentTypeDefinition definition) =>
        definition.GetSettings<CrestContentTypeMenuSettings>()?.ShowInContentMenu == true;

    /// <summary>
    /// Securable types carry their dynamic permission (implied by global ListContent, which the
    /// entry also accepts, so ordinary admins keep seeing it); other types keep the global one.
    /// </summary>
    internal static Crest.Security.Permissions.Permission SelectPermission(ContentTypeDefinition definition) =>
        definition.IsSecurable()
            ? ContentTypePermissionsHelper.CreateDynamicPermission(
                ContentTypePermissionsHelper.PermissionTemplates[CommonPermissions.ListContent.Name], definition)
            : CommonPermissions.ListContent;

    public async Task<IEnumerable<PageEntry>> GetPagesAsync(PageSet declared, CancellationToken cancellationToken = default) =>
        (await contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(ShowsInContentMenu)
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new PageEntry
            {
                Id = $"content-type-{definition.Name.ToLowerInvariant()}",
                Permissions = SelectPermission(definition).Name is var name && name == CommonPermissions.ListContent.Name
                    ? [name]
                    : [name, CommonPermissions.ListContent.Name],
                Menu = new PageMenu(definition.DisplayName, "content", Icon: "@iconify:mdi:file-document-outline")
                {
                    Url = $"/Contents/ContentItems?type={Uri.EscapeDataString(definition.Name)}",
                    CaptionIsData = true,
                },
            })
            .ToArray();
}
