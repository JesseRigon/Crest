using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Interaction;

/// <summary>
/// An overlay menu (§ 4a): rendered only while an open action has it open, at the position the
/// opening event carried; a click outside closes it. Its items are actions (<see cref="MenuAction"/>)
/// whose payload is what opened the menu (the right-clicked row).
/// </summary>
[BlazorComponent("ContextMenu", DisplayName = "Context menu", Category = "Overlays", Icon = "menu_open", IsOverlay = true)]
public partial class ContextMenu : ComponentBase
{
    [Parameter]
    [ComponentZone(Allowed = ["MenuAction"])]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    public DisplayOverlay? Overlay { get; set; }

    private string Position => Overlay?.Position is { } point
        ? FormattableString.Invariant($"left:{point.X}px;top:{point.Y}px;")
        : string.Empty;

    private Task CloseAsync() => Overlay?.CloseAsync() ?? Task.CompletedTask;
}
