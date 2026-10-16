using Grove.Contents.Indexing;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Fields;
using Grove.Infrastructure.Settings;
using Grove.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Data.Indexing;

/// <summary>
/// Search indexing of an option picker field: its selected ids, as keywords. A selection from a
/// nested embedding (a taxonomy's terms) also indexes each selected item's ancestors under
/// <c>{key}.Inherited</c>, so a search for a parent term finds what is filed under its children.
/// </summary>
public sealed class OptionListFieldIndexHandler(IServiceProvider services) : ContentFieldIndexHandler<OptionListField>
{
    public override async Task BuildIndexAsync(OptionListField field, BuildFieldIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        if (field.SelectedIds.Length > 0)
        {
            foreach (var id in field.SelectedIds)
            {
                foreach (var key in context.Keys)
                {
                    context.DocumentIndex.Set(key, id, options);
                }
            }
        }
        else
        {
            foreach (var key in context.Keys)
            {
                context.DocumentIndex.Set(key, ContentIndexingConstants.NullValue, options);
            }
        }

        var sourceKey = string.IsNullOrEmpty(field.SourceKey) ? context.ContentFieldDefinition?.GetSettings<OptionListFieldSettings>().SourceKey : field.SourceKey;
        if (field.SelectedIds.Length == 0 || OptionSourceKeys.Split(sourceKey).Provider != OptionSourceKeys.EmbeddedProvider)
        {
            return;
        }

        var resolver = services.GetRequiredService<IEmbeddedContentItemResolver>();
        foreach (var id in field.SelectedIds)
        {
            if (await resolver.ResolveAsync(id) is not { } reference)
            {
                continue;
            }

            var chain = new List<ContentItem>();
            if (!Ancestry(reference.Parent.Embedded(reference.Embedding), id, reference.Embedding, chain))
            {
                continue;
            }

            foreach (var key in context.Keys)
            {
                foreach (var item in chain)
                {
                    context.DocumentIndex.Set(key + ContentIndexingConstants.InheritedKey, item.ContentItemId, options);
                }
            }
        }
    }

    // The item and its ancestors in a nested embedding, the item first; false when it is not there.
    private static bool Ancestry(IReadOnlyList<ContentItem> items, string id, string embedding, List<ContentItem> chain)
    {
        foreach (var item in items)
        {
            if (item.ContentItemId == id || Ancestry(item.Embedded(embedding), id, embedding, chain))
            {
                chain.Add(item);
                return true;
            }
        }

        return false;
    }
}
