using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Embedding;

/// <summary>
/// The display of a type's embeddings (docs/content-engine.md › Embedded content items): one
/// <c>EmbeddedContentItems</c> shape per embedding holding items, named after the embedding so a
/// tree template places it by name, with the items' displays (in the embedding's display type) as
/// its items. A frozen item of a snapshot embedding displays through the schema stored with it
/// (<see cref="IEmbeddedContentReader"/>). An
/// embedding a module displays itself (<see cref="EmbeddedContentDisplayOptions"/>) is left out.
/// </summary>
public sealed class EmbeddedContentDisplayDriver(
    IContentDefinitionManager definitions,
    IServiceProvider services,
    Microsoft.Extensions.Options.IOptions<EmbeddedContentDisplayOptions> options) : ContentDisplayDriver
{
    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (await ContentDisplayDefinitions.DefinitionAsync(context.Shape, contentItem, definitions) is not { } type || !type.Embeddings.Any())
        {
            return null;
        }

        var results = new List<IDisplayResult>();
        foreach (var embedding in type.Embeddings.Where(embedding => !options.Value.DisplayedElsewhere.Contains($"{type.Name}.{embedding.Name}")))
        {
            var embedded = contentItem.Get<EmbeddedContentItems>(embedding.Name);
            if (embedded is null || embedded.ContentItems.Count == 0)
            {
                continue;
            }

            results.Add(Initialize<EmbeddedContentItemsViewModel>("EmbeddedContentItems", model =>
                {
                    model.Embedding = embedding.Name;
                    model.DisplayName = embedding.DisplayNameOrName;
                })
                .Name(embedding.Name)
                .Differentiator(embedding.Name)
                // The items' displays as the shape's items, built while the shape is processed: the
                // display tree maps them as the component's children.
                .Processing(async shape =>
                {
                    if (shape is not IShape holder)
                    {
                        return;
                    }

                    // Each item displays through its own definition: a frozen item's stored schema.
                    var displayManager = services.GetRequiredService<IContentItemDisplayManager>();
                    var reader = services.GetRequiredService<IEmbeddedContentReader>();
                    foreach (var entry in await reader.ReadAsync(contentItem, type, embedding.Name))
                    {
                        if (embedding.Nested && !embedded.ContentItems.Any(item => item.ContentItemId == entry.Item.ContentItemId))
                        {
                            continue;
                        }

                        await holder.AddAsync(await displayManager.BuildDisplayAsync(entry.Item, entry.Definition, embedding.DisplayType ?? "Summary", context.GroupId));
                    }
                })
                .Location(PlatformConstants.DisplayType.Detail, $"Content:{Position(embedding)}")
                .Location(PlatformConstants.DisplayType.Summary, $"Content:{Position(embedding)}"));
        }

        return results.Count == 0 ? null : Combine(results);
    }

    private static string Position(EmbeddedContentDefinition embedding) =>
        string.IsNullOrWhiteSpace(embedding.Position) ? "10" : embedding.Position;
}

/// <summary>The <c>EmbeddedContentItems</c> shape: which embedding it shows.</summary>
public class EmbeddedContentItemsViewModel
{
    public string Embedding { get; set; }

    public string DisplayName { get; set; }
}
