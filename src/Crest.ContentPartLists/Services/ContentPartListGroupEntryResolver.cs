using Crest.ContentGroups;

namespace Crest.Services;

/// <summary>
/// The "list" content-group entry kind: an Content part list by its logical key, resolved
/// to the ONE content item that holds it (the list is a content item carrying
/// CrestContentPartListPart; its options are part data, not items). Lives here, not
/// in Crest.Server, because Server cannot reference the lists feature - the kind
/// resolver seam is exactly what lets a feature teach the group system a new kind.
/// </summary>
public sealed class ContentPartListGroupEntryResolver(IContentPartListService lists) : IContentGroupEntryResolver
{
    public string Kind => ContentGroupEntryKinds.List;

    public async Task<ContentGroupResolvedEntry?> ResolveAsync(string key, CancellationToken cancellationToken = default)
    {
        var list = await lists.GetAsync(key, cancellationToken);
        return list is null
            ? null
            : new ContentGroupResolvedEntry(Kind, list.Key, list.DisplayText, [], [list.ContentItemId]);
    }
}
