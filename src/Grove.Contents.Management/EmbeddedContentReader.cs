using Grove.Contents.Management.Metadata;

namespace Grove.Contents.Management;

/// <inheritdoc cref="IEmbeddedContentReader"/>
public sealed class EmbeddedContentReader(IContentDefinitionManager definitions) : IEmbeddedContentReader
{
    public async Task<IReadOnlyList<EmbeddedContentEntry>> ReadAsync(ContentItem parent, string embedding)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return await ReadAsync(parent, await definitions.GetTypeDefinitionAsync(parent.ContentType), embedding);
    }

    public async Task<IReadOnlyList<EmbeddedContentEntry>> ReadAsync(ContentItem parent, Metadata.Models.ContentTypeDefinition parentDefinition, string embedding)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var declaration = parentDefinition?.Embeddings
            .FirstOrDefault(candidate => string.Equals(candidate.Name, embedding, StringComparison.OrdinalIgnoreCase));
        if (declaration is null)
        {
            return [];
        }

        var schema = declaration.IsSnapshot ? parent.Get<EmbeddedContentItems>(declaration.Name)?.Schema : null;
        var frozen = schema is null ? null : EmbeddedContentSchema.ReadAll(schema);
        var entries = new List<EmbeddedContentEntry>();
        foreach (var item in declaration.AllItems(parent))
        {
            if (frozen is not null
                && EmbeddedContentSchema.IsFrozen(schema, item.ContentItemId)
                && frozen.TryGetValue(item.ContentType ?? string.Empty, out var stored))
            {
                entries.Add(new EmbeddedContentEntry(item, stored, Frozen: true));
            }
            else if (await definitions.GetTypeDefinitionAsync(item.ContentType) is { } current)
            {
                entries.Add(new EmbeddedContentEntry(item, current, Frozen: false));
            }
        }

        return entries;
    }
}
