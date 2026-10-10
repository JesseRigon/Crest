using Crest.Access;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Records;

namespace Crest.Contents.Security;

/// <summary>
/// What a caller may see of <see cref="ContentItemIndex"/>: every type they may view in full,
/// plus their own items of the types they may view as owner. Replaces the four places that
/// re-derived this per surface (admin lists, GraphQL, pickers, the content-items API).
/// </summary>
public sealed class ContentItemScopeProvider(IContentDefinitionManager contentDefinitions, IAccessDecision decision) : IScopeProvider
{
    public const string TargetName = nameof(ContentItemIndex);

    public string Target => TargetName;

    public int Version => 1;

    public async Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (caller.IsSuperUser)
        {
            return ScopeRule.All;
        }

        var viewAll = new List<object?>();
        var viewOwn = new List<object?>();

        foreach (var definition in await contentDefinitions.ListTypeDefinitionsAsync())
        {
            var any = await decision.DecideAsync(caller, CommonPermissions.ViewContent.Name, new ContentTypeProbe(definition.Name, AsOwner: false), cancellationToken);
            if (any.IsAllowed)
            {
                viewAll.Add(definition.Name);
                continue;
            }

            if (caller.UserId is null)
            {
                continue;
            }

            var own = await decision.DecideAsync(caller, CommonPermissions.ViewContent.Name, new ContentTypeProbe(definition.Name, AsOwner: true), cancellationToken);
            if (own.IsAllowed)
            {
                viewOwn.Add(definition.Name);
            }
        }

        if (viewAll.Count == 0 && viewOwn.Count == 0)
        {
            return ScopeRule.None;
        }

        var anyType = ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentType), viewAll));
        var ownType = ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.In(nameof(ContentItemIndex.ContentType), viewOwn)),
            ScopeFilter.Of(ScopeCondition.Equal(nameof(ContentItemIndex.Owner), caller.UserId)));

        if (viewOwn.Count == 0)
        {
            return ScopeRule.Where(anyType);
        }

        if (viewAll.Count == 0)
        {
            return ScopeRule.Where(ownType);
        }

        return ScopeRule.Where(ScopeFilter.AnyOf(anyType, ownType));
    }
}
