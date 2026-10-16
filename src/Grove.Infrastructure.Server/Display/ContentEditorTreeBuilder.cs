using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Display.Management;
using Grove.Display.Management.Components;

namespace Grove.Display;

/// <summary>
/// Editors as trees (docs/blazor-display.md § 7, rework step 5): an item's editor is built as
/// shapes (a <c>ContentEditor</c> holding the item and its definition, a section per part, the
/// editor component each field's or part's type declares) and mapped once by the tree builder,
/// so a tenant template for <c>ContentEditor__{Type}</c> lays a type's editor out differently,
/// and the node filter applies as everywhere. The page that renders the tree saves what its
/// <c>ContentEditor</c> publishes.
/// </summary>
public sealed class ContentEditorTreeBuilder(
    IContentDefinitionManager definitions,
    IShapeFactory shapeFactory,
    IComponentRegistry registry,
    IDisplayTreeBuilder trees,
    IDisplayTreeFilter filter,
    ICallerContextAccessor callers,
    IServiceProvider services,
    Microsoft.Extensions.Localization.IStringLocalizer<ContentEditorTreeBuilder> S) : IContentEditorTreeBuilder
{
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    /// <summary>The editor tree of <paramref name="item"/> (an existing item, or a new one of its type); null when its type has no definition.</summary>
    public async Task<DisplayTree?> BuildAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        var definition = await definitions.GetTypeDefinitionAsync(item.ContentType);
        if (definition is null)
        {
            return null;
        }

        // The item's content is a dynamic view of its JSON; the conversion yields the JSON itself.
        JsonObject stored = item.Content;
        var editable = (JsonObject)stored.DeepClone();
        var root = await shapeFactory.CreateAsync("ContentEditor", Arguments.From(new Dictionary<string, object?>
        {
            ["ContentType"] = item.ContentType,
            // A new item's id is not its id yet (the create call assigns one): none until saved.
            ["ContentItemId"] = item.Id == 0 ? null : item.ContentItemId,
            ["Content"] = editable,
            ["Definition"] = JsonSerializer.SerializeToNode(Grove.Contents.Types.Api.ContentType.From(definition), s_web),
        }));
        root.Metadata.Alternates.Add("ContentEditor__" + item.ContentType);

        // The labels are the definition's, read through their localization sources
        // (docs/content-engine.md › Localization): attachment names, field labels, embedding names.
        _labels = services.GetService(typeof(Grove.Data.Localization.Data.IDataLocalizer)) as Grove.Data.Localization.Data.IDataLocalizer;

        // Field sets and embeddings in one order: an embedding takes its position among the
        // type's field-set attachments.
        var sections = definition.FieldSets
            .Select(part => (Position: part.GetSettings<ContentTypeFieldSetSettings>().Position ?? string.Empty, Part: part, Embedding: (EmbeddedContentDefinition?)null))
            .Concat(definition.Embeddings.Select(embedding => (Position: embedding.Position ?? string.Empty, Part: (ContentTypeFieldSetDefinition?)null, Embedding: (EmbeddedContentDefinition?)embedding)))
            .OrderBy(section => section.Position, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var (_, sectionPart, sectionEmbedding) in sections)
        {
            if (sectionEmbedding is { } embedding)
            {
                if (await EmbeddingAsync(definition.Name, embedding) is { } embeddingSection)
                {
                    await root.AddAsync(embeddingSection);
                }

                continue;
            }

            var part = sectionPart!;
            var section = await shapeFactory.CreateAsync("EditorSection", Arguments.From(new Dictionary<string, object?>
            {
                ["Title"] = Localized(part.DisplayName(), Grove.Contents.Fields.DataLocalizationContext.ContentFieldSetAttachment(definition.Name)),
            }));
            section.Metadata.Name = part.Name;

            // The common part's editors edit the item's own creation date and owner, when the
            // type shows them; the values travel under CommonFieldSet and the save takes them out.
            if (part.FieldSetDefinition.Name == "Common")
            {
                var common = part.GetSettings<Grove.Contents.Models.CommonFieldSetSettings>();
                if (common.DisplayDateEditor || common.DisplayOwnerEditor)
                {
                    var values = editable[part.Name] as JsonObject ?? [];
                    editable[part.Name] = values;
                    if (common.DisplayDateEditor && registry.TryGet("DateFieldEditor", out var dateEditor))
                    {
                        values["CreatedUtc"] = item.CreatedUtc;
                        await section.AddAsync(await EditorAsync(dateEditor, part.Name, fieldName: null, label: "Created"));
                    }

                    // The owner is a user picked from the users option source (Grove.Contents.OptionPicker's editor).
                    if (common.DisplayOwnerEditor && registry.TryGet("OwnerEditor", out var ownerEditor))
                    {
                        values["Owner"] = item.Owner;
                        values["OwnerName"] = item.Author;
                        await section.AddAsync(await EditorAsync(ownerEditor, part.Name, fieldName: null, label: "Owner"));
                    }
                }
            }

            // A part with its own editor (a title, a body, a permalink) gets it first; a part with
            // no fields but stored values of its own gets the catch-all editor, so nothing the
            // item holds is out of reach.
            if (registry.Components.FirstOrDefault(component => component.Edits.Contains(part.FieldSetDefinition.Name, StringComparer.OrdinalIgnoreCase)) is { } partEditor)
            {
                await section.AddAsync(await EditorAsync(partEditor, part.Name, fieldName: null));
            }
            else if (part.FieldSetDefinition.Fields.Count() == 0
                && stored[part.Name] is JsonObject { Count: > 0 }
                && registry.TryGetEditor(part.FieldSetDefinition.Name, out var fallback))
            {
                await section.AddAsync(await EditorAsync(fallback, part.Name, fieldName: null));
            }

            foreach (var field in part.FieldSetDefinition.Fields.OrderBy(field => field.GetSettings<ContentFieldSettings>().Position ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                // An editor declared for the field's editor setting ("TextField-IconPicker") comes
                // before the field type's own.
                var mode = field.GetSettings<ContentFieldSettings>().Editor;

                // A field its field set's own editor shows, or its system flows keep, has no editor of its own.
                if (string.Equals(mode, ContentFieldEditors.None, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var modeEditor = string.IsNullOrEmpty(mode)
                    ? null
                    : registry.Components.FirstOrDefault(component => component.Edits.Contains($"{field.FieldTypeDefinition.Name}-{mode}", StringComparer.OrdinalIgnoreCase));
                if (modeEditor is not null || registry.TryGetEditor(field.FieldTypeDefinition.Name, out modeEditor))
                {
                    await section.AddAsync(await EditorAsync(modeEditor, part.Name, field.Name,
                        LocalizedOrNull(field.DisplayName(), Grove.Contents.Fields.DataLocalizationContext.ContentField(part.Name))));
                }
            }

            if (section.Items.Count > 0)
            {
                await root.AddAsync(section);
            }
        }

        var tree = await trees.BuildAsync(root, cancellationToken: cancellationToken);
        var caller = callers.Current;
        var filtered = await filter.FilterAsync(tree.Root, caller, cancellationToken) ?? tree.Root;
        return new DisplayTree { Root = filtered, DisplayType = "Edit", ScopeSignature = tree.ScopeSignature };
    }

    // An embedding's section: the editor a component declares for this embedding ("Taxonomy.Terms"),
    // else the embedded content items editor, with the allowed types (stereotypes expanded) and
    // their derived, translated add labels.
    private async Task<IShape?> EmbeddingAsync(string typeName, EmbeddedContentDefinition embedding)
    {
        var section = await shapeFactory.CreateAsync("EditorSection", Arguments.From(new Dictionary<string, object?>
        {
            ["Title"] = Localized(embedding.DisplayNameOrName, Grove.Contents.Fields.DataLocalizationContext.ContentEmbedding(typeName)),
        }));
        section.Metadata.Name = embedding.Name;

        if (registry.Components.FirstOrDefault(component => component.Edits.Contains($"{typeName}.{embedding.Name}", StringComparer.OrdinalIgnoreCase)) is { } declared)
        {
            await section.AddAsync(await EditorAsync(declared, embedding.Name, fieldName: null));
            return section;
        }

        if (!registry.TryGet("EmbeddedContentItemsEditor", out var editor))
        {
            return null;
        }

        var dataLocalizer = _labels;
        var allowed = (await definitions.ListTypeDefinitionsAsync()).Where(embedding.Allows).OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        string Label(ContentTypeDefinition type)
        {
            var name = type.DisplayName ?? type.Name;
            var localized = dataLocalizer?[name, Grove.Contents.Types.DataLocalizationContext.ContentType]?.Value;
            return S["Add {0}", string.IsNullOrEmpty(localized) ? name : localized].Value;
        }

        var shape = await shapeFactory.CreateAsync(editor.Name, Arguments.From(new Dictionary<string, object?>
        {
            ["FieldSetName"] = embedding.Name,
            ["FieldName"] = null,
            ["Label"] = Localized(embedding.DisplayNameOrName, Grove.Contents.Fields.DataLocalizationContext.ContentEmbedding(typeName)),
            ["AllowedTypes"] = allowed.Select(type => type.Name).ToArray(),
            ["AddLabels"] = allowed.Select(Label).ToArray(),
            ["Ordered"] = embedding.Ordered,
            ["MaxCount"] = embedding.MaxCount,
        }));
        shape.Metadata.Name = embedding.Name;
        await section.AddAsync(shape);
        return section;
    }

    private Grove.Data.Localization.Data.IDataLocalizer? _labels;

    // A definition label in the current culture; the label itself when it has no translation.
    private string Localized(string text, string context) => LocalizedOrNull(text, context) ?? text;

    // The translation of a definition label, or null when it has none (the editor then reads the label from the definition).
    private string? LocalizedOrNull(string? text, string context)
    {
        if (string.IsNullOrEmpty(text) || _labels?[text, context]?.Value is not { Length: > 0 } localized || localized == text)
        {
            return null;
        }

        return localized;
    }

    private async Task<IShape> EditorAsync(ComponentDescriptor editor, string partName, string? fieldName, string? label = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["FieldSetName"] = partName,
            ["FieldName"] = fieldName,
        };
        if (label is not null)
        {
            arguments["Label"] = label;
        }

        var shape = await shapeFactory.CreateAsync(editor.Name, Arguments.From(arguments));
        shape.Metadata.Name = fieldName is null ? (label is null ? partName : $"{partName}.{label}") : $"{partName}.{fieldName}";
        return shape;
    }
}
