using System.Text.Json.Nodes;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Grove.Components.Interaction;

/// <summary>
/// A list of items from a data slot (docs/blazor-display.md § 4a): publishes the selected item
/// as an output other nodes bind to (the invoice details read the list's selection), and raises
/// a positioned context-menu event with the right-clicked item as its payload.
/// </summary>
[BlazorComponent("ItemList", DisplayName = "Item list", Category = "Data", Icon = "list")]
public partial class ItemList : ComponentBase
{
    /// <summary>The items: an array, or a query result whose <c>items</c> is one.</summary>
    [Parameter]
    [ComponentParameter(Editor = "dataSource", DisplayName = "Items", Group = "Data")]
    public JsonNode? Items { get; set; }

    /// <summary>The member shown for an item.</summary>
    [Parameter]
    [ComponentParameter(DisplayName = "Text member", Group = "Data")]
    public string TextPath { get; set; } = "displayText";

    [Parameter]
    [ComponentParameter(DisplayName = "Empty text")]
    public string? EmptyText { get; set; }

    [Parameter]
    [ComponentOutput(DisplayName = "Selected item")]
    public EventCallback<JsonNode?> SelectedItem { get; set; }

    /// <summary>Raised on a right-click, with the item and where it happened.</summary>
    [Parameter]
    public EventCallback<ItemPointerEvent> ItemContextMenu { get; set; }

    private int _selected = -1;

    private IReadOnlyList<JsonNode?> Rows => Items switch
    {
        JsonArray array => [.. array],
        JsonObject { } result when result["items"] is JsonArray items => [.. items],
        _ => [],
    };

    private string TextOf(JsonNode? item) =>
        (item is null ? null : DisplayState.Navigate(item, TextPath)) is JsonValue value && value.TryGetValue<string>(out var text) ? text : item?.ToJsonString() ?? string.Empty;

    private async Task SelectAsync(int index, JsonNode? item)
    {
        _selected = index;
        await SelectedItem.InvokeAsync(item?.DeepClone());
    }

    private Task ContextMenuAsync(MouseEventArgs args, JsonNode? item) =>
        ItemContextMenu.InvokeAsync(new ItemPointerEvent(item?.DeepClone(), args.ClientX, args.ClientY));
}

/// <summary>A pointer event on an item: the item and the client position, for an overlay it opens.</summary>
public sealed record ItemPointerEvent(JsonNode? Item, double ClientX, double ClientY) : IDisplayPositioned
{
    public DisplayPoint Position => new(ClientX, ClientY);
}
