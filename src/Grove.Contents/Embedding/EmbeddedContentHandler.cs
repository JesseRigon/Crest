using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Routing;
using Microsoft.Extensions.Localization;

namespace Grove.Contents.Embedding;

/// <summary>
/// Embedded content items (docs/content-engine.md › Embedded content items), on the parent's
/// lifecycle:
/// - the embedded-items accessor: each embedding of the type answers its items;
/// - stable ids: every embedded item, at every depth, has an id from its first save and keeps it;
///   a clone's items get ids of their own;
/// - validation: count limits and allowed types, keyed by the embedding's name;
/// - snapshot embeddings freeze when the parent is published: the schema the items were written in
///   is stored beside them, and the snapshot handlers write what the items keep (copied values).
/// </summary>
public sealed class EmbeddedContentHandler(
    IContentDefinitionManager definitions,
    IContentItemIdGenerator ids,
    IEmbeddedContentReader reader,
    IServiceProvider services,
    IStringLocalizer<EmbeddedContentHandler> S) : ContentHandlerBase
{
    // Resolved when an item freezes, not with the content manager: a snapshot handler reads
    // sources that reach the content manager again.
    private IEnumerable<IEmbeddedContentSnapshotHandler> SnapshotHandlers =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<IEmbeddedContentSnapshotHandler>(services);

    public override async Task GetContentItemAspectAsync(ContentItemAspectContext context)
    {
        if (await definitions.GetTypeDefinitionAsync(context.ContentItem.ContentType) is not { } type || !type.Embeddings.Any())
        {
            return;
        }

        await context.ForAsync<EmbeddedContentItemsAspect>(aspect =>
        {
            foreach (var embedding in type.Embeddings)
            {
                var name = embedding.Name;
                // An item that holds none of the embedding's items reads an empty list, never null.
                Func<JsonObject, JsonArray> accessor = content => content?[name]?["ContentItems"] as JsonArray ?? [];
                aspect.Accessors.Add(accessor);
                if (embedding.Referenceable)
                {
                    aspect.ReferenceableAccessors.Add(accessor);
                }
            }

            return Task.CompletedTask;
        });
    }

    public override Task CreatingAsync(CreateContentContext context) => AssignIdsAsync(context.ContentItem, renew: false);

    public override Task UpdatingAsync(UpdateContentContext context) => AssignIdsAsync(context.ContentItem, renew: false);

    public override Task DraftSavingAsync(SaveDraftContentContext context) => AssignIdsAsync(context.ContentItem, renew: false);

    public override Task ImportingAsync(ImportContentContext context) => AssignIdsAsync(context.ContentItem, renew: false);

    // A clone is another document: its embedded items are its own, so references to the source's
    // items never resolve into the clone.
    public override Task CloningAsync(CloneContentContext context) => AssignIdsAsync(context.CloneContentItem, renew: true);

    public override async Task ValidatingAsync(ValidateContentContext context)
    {
        if (await definitions.GetTypeDefinitionAsync(context.ContentItem.ContentType) is not { } type)
        {
            return;
        }

        foreach (var embedding in type.Embeddings)
        {
            var items = context.ContentItem.Embedded(embedding.Name);
            var label = embedding.DisplayNameOrName;
            if (embedding.MinCount is { } min && items.Count < min)
            {
                context.ContentValidateResult.Fail(new ValidationResult(S["{0} needs at least {1} items.", label, min], [embedding.Name]));
            }

            if (embedding.MaxCount is { } max && items.Count > max)
            {
                context.ContentValidateResult.Fail(new ValidationResult(S["{0} holds at most {1} items.", label, max], [embedding.Name]));
            }

            // Each item checked through the definition it reads through (a frozen item's stored schema).
            var entries = await reader.ReadAsync(context.ContentItem, type, embedding.Name);
            // An item the reader has no definition for has a type that does not exist.
            var known = entries.Select(entry => entry.Item).ToHashSet(ReferenceEqualityComparer.Instance);
            foreach (var contentType in entries.Where(entry => !embedding.Allows(entry.Definition)).Select(entry => entry.Item.ContentType)
                .Concat(embedding.AllItems(context.ContentItem).Where(item => !known.Contains(item)).Select(item => item.ContentType))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                context.ContentValidateResult.Fail(new ValidationResult(S["{0} does not allow '{1}' items.", label, contentType ?? string.Empty], [embedding.Name]));
            }
        }
    }

    public override async Task PublishingAsync(PublishContentContext context)
    {
        if (await definitions.GetTypeDefinitionAsync(context.ContentItem.ContentType) is not { } type)
        {
            return;
        }

        foreach (var embedding in type.Embeddings.Where(embedding => embedding.IsSnapshot))
        {
            await FreezeAsync(context.ContentItem, embedding);
        }
    }

    // Writes the schema of every type the embedding's items have, as it is now, and lets the
    // snapshot handlers write what each item keeps. Items already frozen keep their schema entry.
    private async Task FreezeAsync(ContentItem parent, EmbeddedContentDefinition embedding)
    {
        var embedded = parent.Get<EmbeddedContentItems>(embedding.Name);
        if (embedded is null || embedded.ContentItems.Count == 0)
        {
            return;
        }

        var previous = embedded.Schema;
        var types = new JsonObject();
        foreach (var item in embedded.ContentItems)
        {
            if (string.IsNullOrEmpty(item.ContentType) || types.ContainsKey(item.ContentType))
            {
                continue;
            }

            // An item frozen by an earlier publish keeps the schema it was written in.
            if (EmbeddedContentSchema.IsFrozen(previous, item.ContentItemId)
                && previous?[EmbeddedContentSchema.TypesKey]?[item.ContentType] is JsonObject kept)
            {
                types[item.ContentType] = kept.DeepClone();
                continue;
            }

            if (await definitions.GetTypeDefinitionAsync(item.ContentType) is { } itemType)
            {
                types[item.ContentType] = EmbeddedContentSchema.Write(itemType);
            }
        }

        foreach (var item in embedded.ContentItems)
        {
            if (EmbeddedContentSchema.IsFrozen(previous, item.ContentItemId)
                || types[item.ContentType ?? string.Empty] is not JsonObject entry)
            {
                continue;
            }

            var context = new EmbeddedContentSnapshotContext(parent, embedding, item, EmbeddedContentSchema.Read(item.ContentType, entry));
            foreach (var handler in SnapshotHandlers)
            {
                await handler.FreezeAsync(context);
            }
        }

        parent.AlterEmbedded(embedding.Name, items =>
        {
            items.ContentItems = embedded.ContentItems;
            items.Schema = new JsonObject
            {
                [EmbeddedContentSchema.TypesKey] = types,
                [EmbeddedContentSchema.ItemsKey] = new JsonArray([.. embedded.ContentItems.Select(item => (JsonNode)JsonValue.Create(item.ContentItemId))]),
            };
        });
    }

    // Every embedded item at every depth gets an id when it has none (or a new one when renewing).
    // Written into the item's JSON directly, as the save path writes: an element served from the
    // item's cache may predate the content the save replaced.
    private Task AssignIdsAsync(ContentItem contentItem, bool renew) =>
        AssignIdsAsync((JsonObject)contentItem.Content, contentItem.ContentType, renew, depth: 0);

    private async Task AssignIdsAsync(JsonObject content, string contentType, bool renew, int depth)
    {
        if (depth > EmbeddingDepth.Max || string.IsNullOrEmpty(contentType) || await definitions.GetTypeDefinitionAsync(contentType) is not { } type)
        {
            return;
        }

        foreach (var embedding in type.Embeddings)
        {
            await AssignIdsAsync(content, embedding, renew, depth);
        }
    }

    // The items of one embedding, and of a nested embedding their children too.
    private async Task AssignIdsAsync(JsonObject content, EmbeddedContentDefinition embedding, bool renew, int depth)
    {
        if (content[embedding.Name]?["ContentItems"] is not JsonArray items)
        {
            return;
        }

        foreach (var item in items.OfType<JsonObject>())
        {
            var contentType = item["ContentType"]?.ToString();
            if (renew || string.IsNullOrEmpty(item["ContentItemId"]?.ToString()))
            {
                item["ContentItemId"] = ids.GenerateUniqueId(new ContentItem { ContentType = contentType });
            }

            await AssignIdsAsync(item, contentType, renew, depth + 1);
            if (embedding.Nested)
            {
                await AssignIdsAsync(item, embedding, renew, depth);
            }
        }
    }
}
