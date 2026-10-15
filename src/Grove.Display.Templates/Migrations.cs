using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Display.Templates.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Display.Templates;

/// <summary>
/// The <see cref="TemplatesConstants.ContentType"/> type (drafts, publish and history, edited on
/// the Templates page) and the <see cref="DesignedPagesConstants.ContentType"/> type (decision 9).
/// </summary>
public sealed class Migrations(
    IContentDefinitionManager contentDefinitionManager,
    YesSql.ISession session,
    Services.TemplateStore templates) : DataMigration
{
    private const string LiquidPart = "LiquidPart";

    public async Task<int> CreateAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(nameof(TemplatePart), part => part
            .WithDescription("A Liquid template that emits display-tree nodes."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(TemplatesConstants.ContentType, type => type
            .DisplayedAs("Template")
            .WithPart(nameof(TemplatePart))
            .Draftable()
            .Versionable()
            .Securable());

        await CreateDesignedPagesAsync();
        await CreateDisplayTreePartAsync();

        // A tenant set up now has no LiquidPart to migrate.
        return 4;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreateDesignedPagesAsync();
        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await CreateDisplayTreePartAsync();
        return 3;
    }

    public async Task<int> UpdateFrom3Async()
    {
        await MigrateLiquidPartsAsync();
        return 4;
    }

    /// <summary>
    /// The retired LiquidPart's bodies become DisplayTreePart trees (docs/MVC/shape-engine.md ›
    /// Component coverage; <see cref="Services.LiquidBodies"/>): every type that has the part gets
    /// DisplayTreePart in its place, every version of every item of those types gets its body
    /// converted, a body with Liquid gets a published composite of its own, and the part's
    /// definition is deleted.
    /// </summary>
    private async Task MigrateLiquidPartsAsync()
    {
        var types = (await contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == LiquidPart))
            .ToArray();
        foreach (var type in types)
        {
            var liquid = type.Parts.First(part => part.PartDefinition.Name == LiquidPart);
            var position = liquid.GetSettings<ContentTypePartSettings>().Position;
            await contentDefinitionManager.AlterTypeDefinitionAsync(type.Name, builder =>
            {
                builder.RemovePart(liquid.Name);
                if (!type.Parts.Any(part => part.PartDefinition.Name == nameof(DisplayTreePart)))
                {
                    builder.WithPart(nameof(DisplayTreePart), part => part.WithPosition(position ?? "10"));
                }
            });
        }

        if (types.Length > 0)
        {
            var names = types.Select(type => type.Name).ToArray();
            var composites = new HashSet<string>(StringComparer.Ordinal);
            var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(names)).ListAsync();
            foreach (var item in items)
            {
                JsonObject content = item.Content;
                if (content[LiquidPart] is not JsonObject part)
                {
                    continue;
                }

                var conversion = Services.LiquidBodies.Convert(part["Liquid"]?.ToString(), item.ContentItemId);
                if (content[nameof(DisplayTreePart)]?["Tree"] is null)
                {
                    content[nameof(DisplayTreePart)] = new JsonObject { ["Tree"] = conversion.Tree };
                }

                content.Remove(LiquidPart);
                await session.SaveAsync(item);

                // One composite per item, from its latest body.
                if (conversion.CompositeName is { } composite && item.Latest && composites.Add(composite))
                {
                    var record = await templates.SaveDraftAsync(null, new TemplatePart
                    {
                        Name = composite,
                        IsComposite = true,
                        Source = conversion.CompositeSource ?? string.Empty,
                        DisplayName = $"{item.DisplayText} (body)",
                        Category = "Item bodies",
                        Description = "The Liquid body of an item, kept as a composite when LiquidPart was retired.",
                        Inputs = [new TemplateInput { Name = "ContentItemId", Type = "string" }],
                    });
                    await templates.PublishAsync(record.ContentItemId);
                }
            }
        }

        await contentDefinitionManager.DeletePartDefinitionAsync(LiquidPart);
    }

    // The per-item page body (docs/widgets.md, ruling 2026-10-13): attachable to any type.
    private Task CreateDisplayTreePartAsync() =>
        contentDefinitionManager.AlterPartDefinitionAsync(nameof(DisplayTreePart), part => part
            .Attachable()
            .WithDescription("The item's own page body: a display tree of components, zones and bindings, edited as data."));

    // Drafts and publish, no overrides: a page record is edited directly (decision 9).
    private async Task CreateDesignedPagesAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(nameof(DesignedPagePart), part => part
            .WithDescription("A designed page: a route in a shell bucket and the Liquid tree it renders."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(DesignedPagesConstants.ContentType, type => type
            .DisplayedAs("Designed page")
            .WithPart(nameof(DesignedPagePart))
            .Draftable()
            .Versionable()
            .Securable());
    }
}
