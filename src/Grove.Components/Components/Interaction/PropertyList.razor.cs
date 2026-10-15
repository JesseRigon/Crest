using System.Text.Json.Nodes;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Interaction;

/// <summary>The members of one object as a definition list (a record's details from a slot).</summary>
[BlazorComponent("PropertyList", DisplayName = "Property list", Category = "Data", Icon = "list_alt")]
public partial class PropertyList : ComponentBase
{
    /// <summary>An object, or a query result whose first item is shown.</summary>
    [Parameter]
    [ComponentParameter(Editor = "dataSource", Group = "Data")]
    public JsonNode? Value { get; set; }

    [Parameter]
    public string? EmptyText { get; set; }

    private JsonObject? Record => Value switch
    {
        JsonObject { } result when result["items"] is JsonArray { Count: > 0 } items => items[0] as JsonObject,
        JsonObject obj => obj,
        JsonArray { Count: > 0 } array => array[0] as JsonObject,
        _ => null,
    };

    private static string Text(JsonNode? value) => value switch
    {
        null => string.Empty,
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => text,
        _ => value.ToJsonString(),
    };
}
