#nullable enable
using Grove.Access;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Data.Scoping;
using YesSql;
using YesSql.Services;

namespace Grove.Queries;

/// <summary>
/// Loads the content items a source's rows or hits point at, through the caller's
/// <c>ContentItemIndex</c> rule: an id the scoped query does not return is never loaded, and
/// the items keep the order of the ids.
/// </summary>
public static class ScopedContentItems
{
    public static async Task<IReadOnlyList<ContentItem>> LoadAsync(ISession session, IReadOnlyList<long> documentIds, ScopeRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rule);

        if (documentIds.Count == 0 || rule.Kind == ScopeKind.None)
        {
            return [];
        }

        var ids = documentIds.Distinct().ToArray();
        var admitted = await session.Query<ContentItem, ContentItemIndex>(index => index.DocumentId.IsIn(ids))
            .Where(ScopeExpressions.ToPredicate<ContentItemIndex>(rule))
            .ListAsync(cancellationToken);

        return InOrder(documentIds, admitted, item => item.Id);
    }

    public static async Task<IReadOnlyList<ContentItem>> LoadVersionsAsync(ISession session, IReadOnlyList<string> versionIds, ScopeRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rule);

        if (versionIds.Count == 0 || rule.Kind == ScopeKind.None)
        {
            return [];
        }

        var ids = versionIds.Distinct(StringComparer.Ordinal).ToArray();
        var admitted = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemVersionId.IsIn(ids))
            .Where(ScopeExpressions.ToPredicate<ContentItemIndex>(rule))
            .ListAsync(cancellationToken);

        return InOrder(versionIds, admitted, item => item.ContentItemVersionId);
    }

    /// <summary>The admitted items in the order of the keys; a key with no admitted item is dropped.</summary>
    public static IReadOnlyList<ContentItem> InOrder<TKey>(IReadOnlyList<TKey> keys, IEnumerable<ContentItem> admitted, Func<ContentItem, TKey> key)
        where TKey : notnull
    {
        var byKey = new Dictionary<TKey, ContentItem>();

        foreach (var item in admitted)
        {
            byKey.TryAdd(key(item), item);
        }

        var items = new List<ContentItem>(keys.Count);

        foreach (var candidate in keys)
        {
            if (byKey.TryGetValue(candidate, out var item))
            {
                items.Add(item);
            }
        }

        return items;
    }
}
