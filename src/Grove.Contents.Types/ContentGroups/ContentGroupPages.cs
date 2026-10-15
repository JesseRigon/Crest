#nullable enable

using Grove.Contents;
using Grove.Routing;

namespace Grove.ContentGroups;

/// <summary>
/// When <see cref="GroveContentGroupsSettings.AutoMenuPages"/> is on: one Content-menu entry per
/// visible group (dynamic page registry entries), linking to the generic content-items page
/// filtered by <c>?group=</c>. Sits beside the per-type entries, so the tenant chooses by-type
/// pages, by-group pages, or both.
/// </summary>
public sealed class ContentGroupPages(ContentGroupService groups) : IDynamicPageSource
{
    public async Task<IEnumerable<PageEntry>> GetPagesAsync(PageSet declared, CancellationToken cancellationToken = default)
    {
        if (!(await groups.GetSettingsAsync()).AutoMenuPages)
        {
            return [];
        }

        return (await groups.ListAsync(cancellationToken))
            .Where(group => !group.Hidden && group.Entries.Count > 0)
            .Select(group => new PageEntry
            {
                Id = $"content-group-{group.Key.ToLowerInvariant()}",
                Permissions = [CommonPermissions.ListContent.Name],
                Menu = new PageMenu(group.DisplayName, "content", Icon: "@iconify:mdi:folder-outline")
                {
                    Url = $"/Contents/ContentItems?group={Uri.EscapeDataString(group.Key)}",
                    CaptionIsData = true,
                },
            })
            .ToArray();
    }
}
