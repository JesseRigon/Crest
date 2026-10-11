using Crest.Access;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Records;

namespace Crest.Contents.Security;

/// <summary>
/// What a caller may see of <see cref="ContentItemIndex"/>, per type: the published version
/// where they may view it, the published or latest (draft) version where they may preview or
/// edit it; the own variants of either limited to the items they own. Replaces the four places
/// that re-derived this per surface (admin lists, GraphQL, pickers, the content-items API).
/// </summary>
public sealed class ContentItemScopeProvider(IContentDefinitionManager contentDefinitions, IAccessDecision decision) : IScopeProvider
{
    public const string TargetName = nameof(ContentItemIndex);

    private static readonly string[] s_draftPermissions = [CommonPermissions.PreviewContent.Name, CommonPermissions.EditContent.Name];

    public string Target => TargetName;

    // 2: versions are scoped (Published, or Published or Latest for previewers and editors).
    public int Version => 2;

    public async Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (caller.IsSuperUser)
        {
            return ScopeRule.All;
        }

        var publishedAll = new List<object?>();
        var draftsAll = new List<object?>();
        var publishedOwn = new List<object?>();
        var draftsOwn = new List<object?>();

        foreach (var definition in await contentDefinitions.ListTypeDefinitionsAsync())
        {
            var any = await LevelAsync(caller, definition.Name, asOwner: false, cancellationToken);

            if (any == Level.Drafts)
            {
                draftsAll.Add(definition.Name);
                continue;
            }

            if (any == Level.Published)
            {
                publishedAll.Add(definition.Name);
            }

            if (caller.UserId is null)
            {
                continue;
            }

            // The own variant matters only where it widens what the any-level grants.
            var own = await LevelAsync(caller, definition.Name, asOwner: true, cancellationToken);

            if (own == Level.Drafts)
            {
                draftsOwn.Add(definition.Name);
            }
            else if (own == Level.Published && any == Level.None)
            {
                publishedOwn.Add(definition.Name);
            }
        }

        var members = new List<ScopeFilter>();

        if (publishedAll.Count > 0)
        {
            members.Add(ScopeFilter.AllOf(OfTypes(publishedAll), Published()));
        }

        if (draftsAll.Count > 0)
        {
            members.Add(ScopeFilter.AllOf(OfTypes(draftsAll), PublishedOrLatest()));
        }

        if (publishedOwn.Count > 0)
        {
            members.Add(ScopeFilter.AllOf(OfTypes(publishedOwn), Owned(caller), Published()));
        }

        if (draftsOwn.Count > 0)
        {
            members.Add(ScopeFilter.AllOf(OfTypes(draftsOwn), Owned(caller), PublishedOrLatest()));
        }

        return members.Count switch
        {
            0 => ScopeRule.None,
            1 => ScopeRule.Where(members[0]),
            _ => ScopeRule.Where(ScopeFilter.AnyOf([.. members])),
        };
    }

    private async Task<Level> LevelAsync(CallerContext caller, string contentType, bool asOwner, CancellationToken cancellationToken)
    {
        var probe = new ContentTypeProbe(contentType, asOwner);

        foreach (var permission in s_draftPermissions)
        {
            if ((await decision.DecideAsync(caller, permission, probe, cancellationToken)).IsAllowed)
            {
                return Level.Drafts;
            }
        }

        return (await decision.DecideAsync(caller, CommonPermissions.ViewContent.Name, probe, cancellationToken)).IsAllowed
            ? Level.Published
            : Level.None;
    }

    private static ScopeFilter OfTypes(List<object?> types) =>
        ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentType), types));

    private static ScopeFilter Owned(CallerContext caller) =>
        ScopeFilter.Of(ScopeCondition.Equal(nameof(ContentItemIndex.Owner), caller.UserId));

    private static ScopeFilter Published() =>
        ScopeFilter.Of(ScopeCondition.Equal(nameof(ContentItemIndex.Published), true));

    private static ScopeFilter PublishedOrLatest() =>
        ScopeFilter.AnyOf(Published(), ScopeFilter.Of(ScopeCondition.Equal(nameof(ContentItemIndex.Latest), true)));

    private enum Level
    {
        None,
        Published,
        Drafts,
    }
}
