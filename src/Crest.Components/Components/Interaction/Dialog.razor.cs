using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Interaction;

/// <summary>A modal overlay (§ 4a): rendered only while open, closed by its close button or the backdrop.</summary>
[CrestBlazorComponent("Dialog", DisplayName = "Dialog", Category = "Overlays", Icon = "web_asset", IsOverlay = true)]
public partial class Dialog : ComponentBase
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    public DisplayOverlay? Overlay { get; set; }

    private Task CloseAsync() => Overlay?.CloseAsync() ?? Task.CompletedTask;
}
