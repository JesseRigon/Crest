using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Protocols.GraphQL;

namespace Grove.Contents.GraphQL;

/// <summary>
/// Each embedding is a field on its parent type's object (docs/content-engine.md › GraphQL): the
/// embedded content items, paged, as content items of their own types.
/// </summary>
internal sealed class EmbeddedContentTypeBuilder : IContentTypeBuilder
{
    public void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        foreach (var embedding in contentTypeDefinition.Embeddings)
        {
            var name = embedding.Name.ToCamelCase();
            if (contentItemType.HasField(name))
            {
                continue;
            }

            var embeddingName = embedding.Name;
            contentItemType.Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>(name)
                .Description(embedding.Description ?? $"The {embedding.DisplayNameOrName} embedded content items.")
                .PagingArguments()
                .ResolveAsync(async context =>
                {
                    // Each item with the definition it reads through, attached for its own resolvers;
                    // the parent itself may be a frozen item resolved a level up.
                    var reader = context.RequestServices!.GetRequiredService<IEmbeddedContentReader>();
                    var entries = EmbeddedItemDefinitions.For(context, context.Source) is { } parentDefinition
                        ? await reader.ReadAsync(context.Source, parentDefinition, embeddingName)
                        : await reader.ReadAsync(context.Source, embeddingName);
                    EmbeddedItemDefinitions.Attach(context, entries);
                    var top = context.Source.Embedded(embeddingName).ToHashSet(ReferenceEqualityComparer.Instance);
                    return context.Page(entries.Select(entry => entry.Item).Where(top.Contains));
                });
        }
    }

    public void Clear()
    {
    }
}
