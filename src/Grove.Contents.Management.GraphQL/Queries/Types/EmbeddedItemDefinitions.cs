using System.Runtime.CompilerServices;
using GraphQL;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

/// <summary>
/// The definition each embedded item a query resolved reads through (docs/content-engine.md › Live
/// and snapshot embeddings), attached to the request's resolve context by the embedding field that
/// resolved it (<see cref="IEmbeddedContentReader"/>) and read by the item's own resolvers: a
/// frozen item renders and resolves its embeddings through its stored schema.
/// </summary>
public static class EmbeddedItemDefinitions
{
    private const string Key = "Grove.EmbeddedItemDefinitions";

    public static void Attach(IResolveFieldContext context, IEnumerable<EmbeddedContentEntry> entries)
    {
        if (context.UserContext is null)
        {
            return;
        }

        if (!context.UserContext.TryGetValue(Key, out var value) || value is not ConditionalWeakTable<ContentItem, ContentTypeDefinition> table)
        {
            context.UserContext[Key] = table = new ConditionalWeakTable<ContentItem, ContentTypeDefinition>();
        }

        foreach (var entry in entries)
        {
            table.AddOrUpdate(entry.Item, entry.Definition);
        }
    }

    /// <summary>The definition <paramref name="item"/> was resolved with, or null for an item that is not embedded.</summary>
    public static ContentTypeDefinition For(IResolveFieldContext context, ContentItem item) =>
        context.UserContext is not null
        && context.UserContext.TryGetValue(Key, out var value)
        && value is ConditionalWeakTable<ContentItem, ContentTypeDefinition> table
        && table.TryGetValue(item, out var definition)
            ? definition
            : null;
}
