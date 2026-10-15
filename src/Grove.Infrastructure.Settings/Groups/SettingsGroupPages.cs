#nullable enable

using Grove.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Infrastructure.Settings.Groups;

/// <summary>
/// One page registry entry per registered settings group: the generic settings form at
/// <c>/Settings/{groupId}</c>, gated at that concrete route by the group's permissions, in the
/// menu where the group says. The form's component declares <c>[PageEntry(DeclaredBySource = true)]</c>.
/// </summary>
public sealed class SettingsGroupPages(IOptions<SettingsGroupOptions> options) : IPageSource
{
    public static string RouteOf(string groupId) => "/Settings/" + groupId;

    public IEnumerable<PageEntry> GetPages()
    {
        var declaredBy = GetType().FullName;
        foreach (var group in options.Value.Groups.Values)
        {
            var route = RouteOf(group.Id);
            yield return new PageEntry
            {
                Id = group.EntryId ?? group.Id,
                Routes = [route],
                Permissions = [.. group.Permissions],
                DeclaredBy = declaredBy,
                Menu = new PageMenu(group.Caption, group.Parent, group.Position, group.Icon) { Url = route },
            };
        }
    }
}
