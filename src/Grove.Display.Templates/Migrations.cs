using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Display.Templates.Models;
using YesSql;
using YesSql.Services;
using Grove.Contents.Types;

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
        await contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<TemplateFieldSet>(), part => part
            .WithDescription("A Liquid template that emits display-tree nodes."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(TemplatesConstants.ContentType, type => type
            .DisplayedAs("Template")
            .WithFieldSet(ContentFieldSetName.Of<TemplateFieldSet>())
            .Draftable()
            .Versionable()
            .Securable());

        await CreateDesignedPagesAsync();
        await CreateDisplayTreeFieldSetAsync();

        await DefineTemplateFieldsAsync();

        await DefineDesignedPageFieldsAsync();

        // A tenant set up now has no LiquidPart to migrate.
        return 5;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreateDesignedPagesAsync();
        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await CreateDisplayTreeFieldSetAsync();
        return 3;
    }

    public async Task<int> UpdateFrom3Async()
    {
        await MigrateLiquidPartsAsync();
        return 4;
    }

    /// <summary>
    /// The retired LiquidPart's bodies become DisplayTreeFieldSet trees (docs/MVC/shape-engine.md ›
    /// Component coverage; <see cref="Services.LiquidBodies"/>): every type that has the part gets
    /// DisplayTreeFieldSet in its place, every version of every item of those types gets its body
    /// converted, a body with Liquid gets a published composite of its own, and the part's
    /// definition is deleted.
    /// </summary>
    private async Task MigrateLiquidPartsAsync()
    {
        var types = (await contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.FieldSets.Any(part => part.FieldSetDefinition.Name == LiquidPart))
            .ToArray();
        foreach (var type in types)
        {
            var liquid = type.FieldSets.First(part => part.FieldSetDefinition.Name == LiquidPart);
            var position = liquid.GetSettings<ContentTypeFieldSetSettings>().Position;
            await contentDefinitionManager.AlterTypeDefinitionAsync(type.Name, builder =>
            {
                builder.RemoveFieldSet(liquid.Name);
                if (!type.FieldSets.Any(part => part.FieldSetDefinition.Name == ContentFieldSetName.Of<DisplayTreeFieldSet>()))
                {
                    builder.WithFieldSet(ContentFieldSetName.Of<DisplayTreeFieldSet>(), part => part.WithPosition(position ?? "10"));
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
                if (content[ContentFieldSetName.Of<DisplayTreeFieldSet>()]?["Tree"] is null)
                {
                    content[ContentFieldSetName.Of<DisplayTreeFieldSet>()] = new JsonObject { ["Tree"] = conversion.Tree };
                }

                content.Remove(LiquidPart);
                await session.SaveAsync(item);

                // One composite per item, from its latest body.
                if (conversion.CompositeName is { } composite && item.Latest && composites.Add(composite))
                {
                    var record = await templates.SaveDraftAsync(null, new TemplateFieldSet
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

        await contentDefinitionManager.DeleteFieldSetDefinitionAsync(LiquidPart);
    }

    // The per-item page body (docs/widgets.md, ruling 2026-10-13): attachable to any type.
    private Task CreateDisplayTreeFieldSetAsync() =>
        contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<DisplayTreeFieldSet>(), part => part
            .Attachable()
            .WithDescription("The item's own page body: a display tree of components, zones and bindings, edited as data."));

    // Drafts and publish, no overrides: a page record is edited directly (decision 9).
    private async Task CreateDesignedPagesAsync()
    {
        await contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<DesignedPageFieldSet>(), part => part
            .WithDescription("A designed page: a route in a shell bucket and the Liquid tree it renders."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(DesignedPagesConstants.ContentType, type => type
            .DisplayedAs("Designed page")
            .WithFieldSet(ContentFieldSetName.Of<DesignedPageFieldSet>())
            .Draftable()
            .Versionable()
            .Securable());
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom4Async()
    {
        await DefineTemplateFieldsAsync();
        await DefineDesignedPageFieldsAsync();
        return 5;
    }

    private Task DefineTemplateFieldsAsync() =>
        contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<TemplateFieldSet>(), builder => builder
            .WithField(nameof(TemplateFieldSet.Name), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Name")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.Theme), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Theme")
                .WithPosition("1")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.DisplayType), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("DisplayType")
                .WithPosition("2")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.Source), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Source")
                .WithPosition("3")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.Description), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Description")
                .WithPosition("4")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.IsComposite), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("IsComposite")
                .WithPosition("5")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.DisplayName), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("DisplayName")
                .WithPosition("6")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.Category), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Category")
                .WithPosition("7")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(TemplateFieldSet.Icon), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Icon")
                .WithPosition("8")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));

    private Task DefineDesignedPageFieldsAsync() =>
        contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<DesignedPageFieldSet>(), builder => builder
            .WithField(nameof(DesignedPageFieldSet.Name), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Name")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(DesignedPageFieldSet.Route), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Route")
                .WithPosition("1")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(DesignedPageFieldSet.Bucket), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Bucket")
                .WithPosition("2")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(DesignedPageFieldSet.Title), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Title")
                .WithPosition("3")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(DesignedPageFieldSet.Description), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Description")
                .WithPosition("4")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(DesignedPageFieldSet.AllowAnonymous), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("AllowAnonymous")
                .WithPosition("5")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(DesignedPageFieldSet.Source), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Source")
                .WithPosition("6")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));
}
