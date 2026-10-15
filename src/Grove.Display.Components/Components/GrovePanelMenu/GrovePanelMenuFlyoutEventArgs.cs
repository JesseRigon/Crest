using Microsoft.AspNetCore.Components.Web;

namespace Grove.Display.Components.Navigation;

public sealed class GrovePanelMenuFlyoutEventArgs(GrovePanelMenuItem item, MouseEventArgs mouseEvent)
{
    public GrovePanelMenuItem Item { get; } = item;

    public MouseEventArgs MouseEvent { get; } = mouseEvent;
}
