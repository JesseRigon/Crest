using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Records;
using Crest.Contents.Security;
using Crest.Sharing;
using YesSql;

namespace Crest.Contents.Sharing;

/// <summary>
/// The access level each content permission takes when sharing decides content (docs/sharing.md
/// § 6). Viewing is Viewer. Previewing (drafts) and editing are Editor. Publishing is Manager:
/// the platform already ranks publishing above editing (<c>PublishContent</c> implies
/// <c>EditContent</c>), and publishing is what shows an item to its Viewers, including the public.
/// Editors keep their split from publishers: they write and preview drafts, and a Manager makes
/// them live. Deleting is Manager. Each permission's own variant (<c>ViewOwnContent</c>) and
/// per-type variant (<c>View_{Type}</c>, <c>ViewOwn_{Type}</c>) take the same level; ownership
/// is sharing's (the owner holds Manager). Cloning, listing and changing the owner are not mapped:
/// the content mapper and role permissions still decide them.
/// </summary>
public static class ContentShareLevels
{
    private static readonly (string Base, string Own, string Template, string OwnTemplate, AccessLevel Level)[] s_levels =
    [
        ("ViewContent", "ViewOwnContent", "View_", "ViewOwn_", AccessLevel.Viewer),
        ("PreviewContent", "PreviewOwnContent", "Preview_", "PreviewOwn_", AccessLevel.Editor),
        ("EditContent", "EditOwnContent", "Edit_", "EditOwn_", AccessLevel.Editor),
        ("PublishContent", "PublishOwnContent", "Publish_", "PublishOwn_", AccessLevel.Manager),
        ("DeleteContent", "DeleteOwnContent", "Delete_", "DeleteOwn_", AccessLevel.Manager),
    ];

    public static AccessLevel? LevelFor(string permission)
    {
        foreach (var (name, own, template, ownTemplate, level) in s_levels)
        {
            if (permission == name || permission == own ||
                (permission.Length > template.Length && permission.StartsWith(template, StringComparison.Ordinal)) ||
                (permission.Length > ownTemplate.Length && permission.StartsWith(ownTemplate, StringComparison.Ordinal)))
            {
                return level;
            }
        }

        return null;
    }
}

/// <summary>What the content kinds read about items and types; the store's by default.</summary>
public interface IContentShareLookup
{
    /// <summary>The latest version's type and owner; null when the item does not exist.</summary>
    Task<(string ContentType, string? Owner)?> ItemAsync(string contentItemId);

    Task<IReadOnlyList<string>> ItemsOfTypeAsync(string contentType);

    Task<IReadOnlyList<string>> TypesAsync();

    Task<bool> TypeExistsAsync(string contentType);
}

public sealed class ContentShareLookup(ISession session, IContentDefinitionManager definitions) : IContentShareLookup
{
    public async Task<(string ContentType, string? Owner)?> ItemAsync(string contentItemId)
    {
        var row = await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId == contentItemId && index.Latest).FirstOrDefaultAsync();
        return row is null ? null : (row.ContentType, row.Owner);
    }

    public async Task<IReadOnlyList<string>> ItemsOfTypeAsync(string contentType) =>
        [.. (await session.QueryIndex<ContentItemIndex>(index => index.ContentType == contentType && index.Latest).ListAsync())
            .Select(index => index.ContentItemId)
            .Distinct(StringComparer.Ordinal)];

    public async Task<IReadOnlyList<string>> TypesAsync() =>
        [.. (await definitions.ListTypeDefinitionsAsync()).Select(definition => definition.Name)];

    public async Task<bool> TypeExistsAsync(string contentType) =>
        await definitions.GetTypeDefinitionAsync(contentType) is not null;
}

/// <summary>
/// A content type as a shareable object (requirement 73): the root of its items' chain
/// (tenant → content type → item). Its entries (set on the type, in recipes with the
/// <c>Sharing</c> step) flow down to every item of the type. A content permission asked against
/// the type's name, or a <see cref="ContentTypeProbe"/>, is about the type: creating an item takes
/// Editor on its type.
/// </summary>
public sealed class ContentTypeShareableKind(IContentShareLookup lookup) : IShareableKind, IShareableTree, IShareableRoots
{
    public const string KindName = "contenttype";

    public string Name => KindName;

    public bool TryIdentify(object resource, out string id)
    {
        switch (resource)
        {
            case ContentTypeProbe probe:
                id = probe.ContentType;
                return true;
            case string type when !string.IsNullOrEmpty(type):
                id = type;
                return true;
            default:
                id = string.Empty;
                return false;
        }
    }

    public async Task<ShareableObject?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        await lookup.TypeExistsAsync(id)
            ? new ShareableObject
            {
                Ref = new ShareableRef(KindName, id),
                Facts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["contenttype"] = [id] },
            }
            : null;

    public AccessLevel? LevelFor(string permission) => ContentShareLevels.LevelFor(permission);

    public async Task<IReadOnlyList<ShareableRef>> ChildrenAsync(string id, CancellationToken cancellationToken = default) =>
        [.. (await lookup.ItemsOfTypeAsync(id)).Select(item => new ShareableRef(ContentShareableKind.KindName, item))];

    public async Task<IReadOnlyList<ShareableRef>> RootsAsync(CancellationToken cancellationToken = default) =>
        [.. (await lookup.TypesAsync()).Select(type => new ShareableRef(KindName, type))];
}

/// <summary>
/// Content items as shareable objects (requirement 73): each item's parent is its content type;
/// its owner holds Manager on it; rules test its <c>contenttype</c>. Lists of
/// <see cref="ContentItemIndex"/> filter on the access index by <c>ContentItemId</c>. An item
/// not saved yet (being created) has no owner: the decision is its type's, so creating takes
/// Editor on the type. Requirement 74 (an item placed in a folder follows the folder's chain)
/// waits for content and media placements to be linked.
/// </summary>
public sealed class ContentShareableKind(IContentShareLookup lookup) : IShareableKind, IShareableScopeTarget
{
    public const string KindName = "content";

    // Items asked about this request: one being created is not in the index yet.
    private readonly Dictionary<string, ContentItem> _seen = new(StringComparer.Ordinal);

    // Items a handler saved this request: newer than the index until the session flushes.
    private readonly Dictionary<string, ContentItem> _saved = new(StringComparer.Ordinal);

    public string Name => KindName;

    public ShareScopeTarget ScopeTarget { get; } = new(nameof(ContentItemIndex), nameof(ContentItemIndex.ContentItemId));

    public bool TryIdentify(object resource, out string id)
    {
        if (resource is ContentItem { ContentItemId: { Length: > 0 } itemId } item)
        {
            _seen[itemId] = item;
            id = itemId;
            return true;
        }

        id = string.Empty;
        return false;
    }

    /// <summary>Tells the kind about an item that was saved (a handler), which it may not find
    /// in the index yet or find there with an old owner.</summary>
    public void Remember(ContentItem item)
    {
        if (!string.IsNullOrEmpty(item.ContentItemId))
        {
            _saved[item.ContentItemId] = item;
        }
    }

    public async Task<ShareableObject?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        string contentType;
        string? owner;
        if (_saved.TryGetValue(id, out var saved) && !string.IsNullOrEmpty(saved.ContentType))
        {
            (contentType, owner) = (saved.ContentType, saved.Owner);
        }
        else if (await lookup.ItemAsync(id) is { } stored)
        {
            (contentType, owner) = stored;
        }
        else if (_seen.TryGetValue(id, out var unsaved) && !string.IsNullOrEmpty(unsaved.ContentType))
        {
            // Not saved yet: nobody owns it until it exists, so its type decides.
            (contentType, owner) = (unsaved.ContentType, null);
        }
        else
        {
            return null;
        }

        return new ShareableObject
        {
            Ref = new ShareableRef(KindName, id),
            Parent = new ShareableRef(ContentTypeShareableKind.KindName, contentType),
            OwnerUserId = string.IsNullOrEmpty(owner) ? null : owner,
            Facts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["contenttype"] = [contentType] },
        };
    }

    public AccessLevel? LevelFor(string permission) => ContentShareLevels.LevelFor(permission);
}
