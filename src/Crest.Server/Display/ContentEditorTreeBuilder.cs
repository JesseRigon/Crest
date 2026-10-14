using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Metadata.Settings;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Components;

namespace Crest.Display;

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
    ICallerContextAccessor callers)
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
        var root = await shapeFactory.CreateAsync("ContentEditor", Arguments.From(new Dictionary<string, object?>
        {
            ["ContentType"] = item.ContentType,
            ["Content"] = stored.DeepClone(),
            ["Definition"] = JsonSerializer.SerializeToNode(Crest.ContentTypes.Api.ContentType.From(definition), s_web),
        }));
        root.Metadata.Alternates.Add("ContentEditor__" + item.ContentType);

        foreach (var part in definition.Parts.OrderBy(part => part.GetSettings<ContentTypePartSettings>().Position ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var section = await shapeFactory.CreateAsync("EditorSection", Arguments.From(new Dictionary<string, object?>
            {
                ["Title"] = part.DisplayName(),
            }));
            section.Metadata.Name = part.Name;

            // A part with its own editor (a title, a body, a permalink) gets it first; a part with
            // no fields but stored values of its own gets the catch-all editor, so nothing the
            // item holds is out of reach.
            if (registry.Components.FirstOrDefault(component => component.Edits.Contains(part.PartDefinition.Name, StringComparer.OrdinalIgnoreCase)) is { } partEditor)
            {
                await section.AddAsync(await EditorAsync(partEditor, part.Name, fieldName: null));
            }
            else if (part.PartDefinition.Fields.Count() == 0
                && stored[part.Name] is JsonObject { Count: > 0 }
                && registry.TryGetEditor(part.PartDefinition.Name, out var fallback))
            {
                await section.AddAsync(await EditorAsync(fallback, part.Name, fieldName: null));
            }

            foreach (var field in part.PartDefinition.Fields.OrderBy(field => field.GetSettings<ContentPartFieldSettings>().Position ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (registry.TryGetEditor(field.FieldDefinition.Name, out var editor))
                {
                    await section.AddAsync(await EditorAsync(editor, part.Name, field.Name));
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

    private async Task<IShape> EditorAsync(ComponentDescriptor editor, string partName, string? fieldName)
    {
        var shape = await shapeFactory.CreateAsync(editor.Name, Arguments.From(new Dictionary<string, object?>
        {
            ["PartName"] = partName,
            ["FieldName"] = fieldName,
        }));
        shape.Metadata.Name = fieldName is null ? partName : $"{partName}.{fieldName}";
        return shape;
    }
}
