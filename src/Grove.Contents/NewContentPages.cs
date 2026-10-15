using Grove.Admin.Models;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.Contents.Security;
using Grove.Routing;
using Grove.Settings;

namespace Grove.Contents;

/// <summary>
/// The New branch (dynamic page registry entries, since content types are tenant data): one
/// item per creatable content type, opening the native create page for that type and gated at
/// that concrete route by the type's own create permission, so the menu and the gate agree. The
/// branch is locked: the tenant's menu edits cannot move items into or out of it.
/// </summary>
public sealed class NewContentPages(IContentDefinitionManager contentDefinitionManager, ISiteService siteService) : IDynamicPageSource
{
    public const string NewEntryId = "new";

    public async Task<IEnumerable<PageEntry>> GetPagesAsync(PageSet declared, CancellationToken cancellationToken = default)
    {
        if (!(await siteService.GetSettingsAsync<AdminSettings>()).DisplayNewMenu)
        {
            return [];
        }

        var creatable = (await contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(definition => definition.IsCreatable())
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (creatable.Length == 0)
        {
            return [];
        }

        return
        [
            new PageEntry
            {
                Id = NewEntryId,
                Menu = new PageMenu("New", Position: "-1", Icon: "@iconify:mdi:plus-circle") { Locked = true },
            },
            .. creatable.Select(definition => new PageEntry
            {
                Id = $"new-{definition.Name.ToLowerInvariant()}",
                Routes = [$"/Contents/ContentTypes/{definition.Name}/Create"],
                Permissions = CreatePermissions(definition),
                Menu = new PageMenu(definition.DisplayName, NewEntryId, "5")
                {
                    Url = $"/Contents/ContentTypes/{Uri.EscapeDataString(definition.Name)}/Create",
                    CaptionIsData = true,
                },
            }),
        ];
    }

    // The type's own "edit own" permission, or the global ones that imply it.
    private static string[] CreatePermissions(ContentTypeDefinition definition) =>
    [
        ContentTypePermissionsHelper.CreateDynamicPermission(
            ContentTypePermissionsHelper.PermissionTemplates[CommonPermissions.EditOwnContent.Name], definition).Name,
        CommonPermissions.EditOwnContent.Name,
        CommonPermissions.EditContent.Name,
    ];
}
