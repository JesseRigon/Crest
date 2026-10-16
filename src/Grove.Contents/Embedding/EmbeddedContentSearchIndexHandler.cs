using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Indexing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Embedding;

/// <summary>
/// Adds embedded content items' fields to the parent's search document under the embedding's path
/// (docs/content-engine.md › Embedded content items), so a search finds the parent by embedded
/// content: each embedded item is indexed by every document index handler with its keys under
/// <c>{key}.{Embedding}</c>. Nested embeddings descend the same way.
/// </summary>
public sealed class EmbeddedContentSearchIndexHandler(IServiceProvider services) : IDocumentIndexHandler
{
    public async Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ContentItem contentItem)
        {
            return;
        }

        var type = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(contentItem.ContentType);
        if (type is null || !type.Embeddings.Any())
        {
            return;
        }

        // The field sets' and fields' handlers (which key their values by the context's keys) and
        // this one for nested embeddings; the item-level handlers write the parent's own keys.
        // Resolved here, not injected: the handlers include this one.
        var handlers = services.GetServices<IDocumentIndexHandler>()
            .Where(handler => handler is Indexing.ContentItemIndexCoordinator or EmbeddedContentSearchIndexHandler)
            .ToArray();
        // The parent's own document (not a nested item's) also stores each embedding whole, with
        // its declaration, for engines that map embeddings as complex fields (Azure AI Search).
        var root = !context.Keys.Any(key => key.Contains('.'));
        foreach (var embedding in type.Embeddings)
        {
            if (root)
            {
                context.DocumentIndex.Set(embedding.Name, contentItem.Get<EmbeddedContentItems>(embedding.Name) ?? new EmbeddedContentItems(), DocumentIndexOptions.Store, new Dictionary<string, object>
                {
                    [nameof(EmbeddedContentDefinition)] = embedding,
                });
            }

            var items = embedding.AllItems(contentItem).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var keys = context.Keys.Select(key => $"{key}.{embedding.Name}").DefaultIfEmpty(embedding.Name).ToList();
            foreach (var item in items)
            {
                var itemContext = new BuildDocumentIndexContext(context.DocumentIndex, item, keys, context.Settings);
                foreach (var handler in handlers)
                {
                    await handler.BuildIndexAsync(itemContext);
                }
            }
        }
    }
}
