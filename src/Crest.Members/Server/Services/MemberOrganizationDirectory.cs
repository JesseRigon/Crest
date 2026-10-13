using System.Text;
using Crest.ContentManagement;
using Crest.Members.Models;
using Crest.Routing;
using Microsoft.AspNetCore.Http;

namespace Crest.Members.Services;

/// <summary>
/// The organizations a member is bound to, as the member's own session shows them: the display
/// name and the slug member page paths carry (<c>/{member prefix}/{slug}/…</c>), resolved
/// server-side.
/// </summary>
/// <remarks>
/// The session hands the member shell names, not organization ids to look up. A member is
/// bound to an organization; that does not make the organization's content item something
/// they may read through the content APIs (see docs/media.md), so the server, which already
/// knows the bindings, supplies the fields the shell displays.
///
/// <para>
/// A slug is derived from the organization's name and is unique among THIS member's
/// organizations, never tenant-wide: it is only ever resolved against the signed-in member's
/// own bindings (<see cref="MemberOrganizationSlugResolver"/>), so a path can never name an
/// organization the member may not act for. A name that collides with an organization-free
/// page of the member shell (login, the picker, …) or with another of the member's
/// organizations is suffixed with the start of the organization's id; the id itself always
/// resolves too, so a bookmark survives a rename that way.
/// </para>
/// </remarks>
public sealed class MemberOrganizationDirectory(IContentManager contentManager, IRouteComponentTableManager routeTables)
{
    // The shell's own infrastructure segments: a slug never shadows them.
    private static readonly string[] s_infrastructure = ["api", "_framework", "_content", "_blazor"];

    public async Task<IReadOnlyList<MemberSessionOrganization>> GetAsync(MemberModel? member)
    {
        var ids = member?.Bindings.Select(binding => binding.OrganizationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
        if (ids.Length == 0)
        {
            return [];
        }

        var items = await contentManager.GetAsync(ids, VersionOptions.Published);
        var names = items.ToDictionary(item => item.ContentItemId, item => item.DisplayText, StringComparer.Ordinal);
        var taken = new HashSet<string>(await ReservedSegmentsAsync(), StringComparer.OrdinalIgnoreCase);

        var organizations = new List<MemberSessionOrganization>(ids.Length);
        foreach (var id in ids)
        {
            var name = names.TryGetValue(id, out var displayText) && !string.IsNullOrWhiteSpace(displayText) ? displayText : id;
            var slug = Slugify(name);
            if (slug.Length == 0 || taken.Contains(slug))
            {
                slug = (slug.Length == 0 ? "org" : slug) + "-" + id[..Math.Min(6, id.Length)].ToLowerInvariant();
            }

            taken.Add(slug);
            organizations.Add(new MemberSessionOrganization(id, name, slug));
        }

        return organizations;
    }

    /// <summary>
    /// The organization a slug (or an organization id) names among the member's own
    /// organizations, or null.
    /// </summary>
    public async Task<string?> ResolveSlugAsync(MemberModel? member, string slug)
    {
        foreach (var organization in await GetAsync(member))
        {
            if (string.Equals(organization.Slug, slug, StringComparison.OrdinalIgnoreCase)
                || string.Equals(organization.Id, slug, StringComparison.Ordinal))
            {
                return organization.Id;
            }
        }

        return null;
    }

    internal static string Slugify(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark
                && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    // The first segment of every organization-free member page ("login", "organizations"),
    // read from the member route table rather than re-typed here.
    private async Task<IEnumerable<string>> ReservedSegmentsAsync()
    {
        var table = await routeTables.GetRouteComponentTableAsync();
        return table.Entries
            .Where(entry => entry.Shell == ShellId.Member && entry.IsOrganizationFree)
            .Select(entry => entry.RoutePattern.Trim('/').Split('/')[0])
            .Where(segment => segment.Length > 0 && !segment.StartsWith('{'))
            .Concat(s_infrastructure);
    }
}

/// <summary>
/// The member shell's organization slugs (<see cref="IShellOrganizationResolver"/>): a slug
/// resolves only among the signed-in user's own member bindings.
/// </summary>
public sealed class MemberOrganizationSlugResolver(IMemberService members, MemberOrganizationDirectory directory) : IShellOrganizationResolver
{
    public async Task<string?> ResolveAsync(HttpContext context, string slug)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return null;
        }

        return await directory.ResolveSlugAsync(await members.GetAsync(userId, context.RequestAborted), slug);
    }

    public async Task<IReadOnlyList<ShellOrganization>> ListAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return [];
        }

        return
        [
            .. (await directory.GetAsync(await members.GetAsync(userId, context.RequestAborted)))
                .Select(organization => new ShellOrganization(organization.Id, organization.Name, organization.Slug))
        ];
    }
}
