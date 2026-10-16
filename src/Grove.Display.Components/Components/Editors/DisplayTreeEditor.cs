using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Display.Components.Primitives;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Display.Components.Editors;

/// <summary>
/// The editor of an item's own page body (docs/widgets.md, ruling 2026-10-13: the
/// <c>DisplayTreeFieldSet</c>): the tree as JSON until the designer canvas is scoped to one item.
/// Every edit is checked like a template (<see cref="DisplayTreeValidator.ValidateStored"/>:
/// registered components, properties that fit the schema, sound wiring; nothing in it can be
/// code), the problems shown under it; the server checks again when the item is saved.
/// </summary>
[BlazorComponent("DisplayTreeEditor", DisplayName = "Page body", Category = "Editors", Icon = "dashboard_customize", Edits = ["DisplayTree"])]
public sealed class DisplayTreeEditor : FieldEditorBase
{
    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    private IReadOnlyList<string> _problems = [];

    [Inject]
    private IComponentRegistry Registry { get; set; } = null!;

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GroveTextArea>(0);
        builder.AddComponentParameter(1, nameof(GroveTextArea.Value), Value?["Tree"]?.ToJsonString(s_indented) ?? string.Empty);
        builder.AddComponentParameter(2, nameof(GroveTextArea.Change), EventCallback.Factory.Create<string>(this, Replace));
        builder.AddComponentParameter(3, nameof(GroveTextArea.Disabled), Disabled);
        builder.AddComponentParameter(4, nameof(GroveTextArea.Rows), 16);
        builder.AddComponentParameter(5, nameof(GroveTextArea.Style), "width: 100%; font-family: var(--grove-font-mono, monospace);");
        builder.CloseComponent();
        foreach (var problem in _problems)
        {
            builder.OpenElement(6, "div");
            builder.AddAttribute(7, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(8, problem);
            builder.CloseElement();
        }
    }

    private void Replace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _problems = [];
            Set("Tree", null);
            return;
        }

        JsonNode? json;
        try
        {
            json = JsonNode.Parse(text);
        }
        catch (JsonException exception)
        {
            _problems = [exception.Message];
            return;
        }

        if (!DisplayTreeJson.TryParse(json, out var root, out var error))
        {
            _problems = [error ?? "The tree does not parse."];
            return;
        }

        _problems = [.. DisplayTreeValidator.ValidateStored(root!, Registry).Select(diagnostic => $"{diagnostic.Path}: {diagnostic.Reason}")];
        Set("Tree", json);
    }
}
