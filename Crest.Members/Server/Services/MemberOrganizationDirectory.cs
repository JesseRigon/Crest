using Crest.Members.Models;
using Crest.ContentManagement;

namespace Crest.Members.Services;

/// <summary>
/// The display names of the organizations a member is bound to, resolved server-side for
/// the member's own session.
/// </summary>
/// <remarks>
/// The session hands the member shell names, not organization ids to look up. A member is
/// bound to an organization; that does not make the organization's content item something
/// they may read through the content APIs (see docs/media.md), so the
/// server, which already knows the bindings, supplies the one field the shell displays.
/// </remarks>
public sealed class MemberOrganizationDirectory(IContentManager contentManager)
{
    public async Task<IReadOnlyList<MemberSessionOrganization>> GetAsync(MemberModel? member)
    {
        var ids = member?.Bindings.Select(binding => binding.OrganizationId).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (ids.Length == 0)
        {
            return [];
        }

        var items = await contentManager.GetAsync(ids, VersionOptions.Published);
        var names = items.ToDictionary(item => item.ContentItemId, item => item.DisplayText, StringComparer.Ordinal);

        return
        [
            .. ids.Select(id => new MemberSessionOrganization(
                id,
                names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : id))
        ];
    }
}
