using System.Text.Json.Nodes;
using Grove.Display.Management.Components;
using Grove.Display.Templates.Models;
using Microsoft.AspNetCore.Components;

namespace Grove.Display.Templates.Services;

/// <summary>
/// Composites as components (rulings 2026-10-12, custom UI at runtime): a composite template's
/// declaration as the registry's descriptor, so the palette, the schema endpoint and validation
/// treat it like a coded component. It has no type: the evaluator expands it where it is placed.
/// </summary>
public static class TemplateComposites
{
    public const string Module = "Templates";

    public static ComponentDescriptor Describe(TemplateRecord template) => new(
        template.Name,
        Type: null,
        [
            .. template.Inputs.Select(input =>
            {
                var (kind, editor, type) = Kind(input.Type);
                return new ParameterDescriptor(
                    input.Name,
                    type,
                    input.Choices is { Length: > 0 } ? ParameterKind.Choice : kind,
                    input.Choices is { Length: > 0 } ? ComponentSchema.Editors.Enum : editor,
                    input.DisplayName,
                    Required: input.Required,
                    Choices: input.Choices);
            }),
            .. template.Outputs.Select(output => new ParameterDescriptor(
                output.Name,
                typeof(EventCallback<JsonNode?>),
                ParameterKind.Output,
                ComponentSchema.Editors.Output,
                output.DisplayName,
                ValueType: typeof(JsonNode))),
        ],
        [],
        ComponentSchema.AllShells,
        Module: Module,
        DisplayName: template.DisplayName ?? template.Name,
        Category: template.Category ?? "Composites",
        Icon: template.Icon ?? "widgets");

    public static (ParameterKind Kind, string Editor, Type Type) Kind(string? type) => (type ?? "string").ToLowerInvariant() switch
    {
        "number" => (ParameterKind.Number, ComponentSchema.Editors.Number, typeof(decimal?)),
        "boolean" => (ParameterKind.Boolean, ComponentSchema.Editors.Bool, typeof(bool)),
        "object" => (ParameterKind.Object, ComponentSchema.Editors.Object, typeof(JsonObject)),
        "array" => (ParameterKind.Array, ComponentSchema.Editors.List, typeof(JsonArray)),
        _ => (ParameterKind.String, ComponentSchema.Editors.Text, typeof(string)),
    };
}
