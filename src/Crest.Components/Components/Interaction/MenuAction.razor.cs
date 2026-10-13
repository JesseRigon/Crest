using System.Text.Json.Nodes;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Interaction;

/// <summary>
/// One item of a menu (§ 4a): its <see cref="Click"/> event runs the actions bound to it with the
/// payload that opened the enclosing overlay (<c>event</c> is the right-clicked row), then closes
/// the overlay. An item whose workflow the viewer may not run is not in the tree at all.
/// </summary>
[CrestBlazorComponent("MenuAction", DisplayName = "Menu action", Category = "Overlays", Icon = "touch_app")]
public partial class MenuAction : ComponentBase
{
    [Parameter]
    [ComponentParameter(Required = true)]
    public string? Text { get; set; }

    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    public EventCallback<JsonNode?> Click { get; set; }

    [CascadingParameter]
    public DisplayOverlay? Overlay { get; set; }

    private async Task ClickAsync()
    {
        await Click.InvokeAsync(Overlay?.Context?.DeepClone());
        if (Overlay is { } overlay)
        {
            await overlay.CloseAsync();
        }
    }
}
