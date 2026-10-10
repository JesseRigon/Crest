using Crest.Access;
using Crest.ContentManagement.Records;
using Crest.Indexing;
using YesSql;

namespace Crest.Services;

/// <summary>
/// On the member side (and for an organization system actor) a caller sees only the rows
/// of their organization among the organization-scoped types; every other type passes
/// through. A member-side caller with no organization sees none of them.
/// </summary>
public sealed class OrganizationScopeProvider(OrganizationScopedTypes scopedTypes, ISession session) : IScopeProvider
{
    public string Target => nameof(ContentItemIndex);

    public int Version => 1;

    public async Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var scoped = scopedTypes.Types.Cast<object?>().ToList();
        if (scoped.Count == 0 || caller.IsSuperUser)
        {
            return ScopeRule.All;
        }

        if (caller.Side is not (CallerSide.Member or CallerSide.System) || caller.OrganizationId is null)
        {
            if (caller.UserClass == CallerClasses.Member)
            {
                // A member off the member side (a signed-in visitor of the public site) sees
                // none of the organization-scoped rows.
                return ScopeRule.Where(NotScoped(scoped));
            }

            if (caller.Side is CallerSide.Admin or CallerSide.Site or CallerSide.System)
            {
                // Staff and visitors are not organization-bound; the content scope decides.
                return ScopeRule.All;
            }

            // A member with no organization in this request sees none of the scoped rows.
            return ScopeRule.Where(NotScoped(scoped));
        }

        var ids = (await session
            .QueryIndex<CrestOrganizationIndex>(index => index.OrganizationId == caller.OrganizationId && index.Latest)
            .ListAsync())
            .Select(index => (object?)index.ContentItemId)
            .ToList();

        if (ids.Count == 0)
        {
            return ScopeRule.Where(NotScoped(scoped));
        }

        var own = ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentType), scoped)),
            ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentItemId), ids)));

        return ScopeRule.Where(ScopeFilter.AnyOf(NotScoped(scoped), own));
    }

    private static ScopeFilter NotScoped(List<object?> scoped) =>
        ScopeFilter.Of(ScopeCondition.NotIn(nameof(ContentItemIndex.ContentType), scoped));
}

/// <summary>Which content types carry <see cref="Models.CrestOrganizationPart"/> and are scoped by
/// organization; a deployment decision declared once (a singleton), like assignment scoping.</summary>
public sealed class OrganizationScopedTypes
{
    private readonly HashSet<string> _types = new(StringComparer.OrdinalIgnoreCase);

    public OrganizationScopedTypes Add(string contentType)
    {
        _types.Add(contentType);
        return this;
    }

    public bool IsScoped(string contentType) => _types.Contains(contentType);

    public IEnumerable<string> Types => _types;
}
