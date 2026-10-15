#nullable enable

using Grove.Contents.Management.Metadata;

namespace Grove.ContentGroups;

/// <summary>The built-in entry kind: a content type, covering every item of it.</summary>
public sealed class ContentTypeGroupEntryResolver(IContentDefinitionManager contentDefinitionManager) : IContentGroupEntryResolver
{
    public string Kind => ContentGroupEntryKinds.Type;

    public async Task<ContentGroupResolvedEntry?> ResolveAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(key);
        return definition is null
            ? null
            : new ContentGroupResolvedEntry(Kind, definition.Name, definition.DisplayName, [definition.Name], []);
    }
}
