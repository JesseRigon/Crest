namespace Grove.Display.Components.Navigation;

public sealed record GrovePanelMenuActionIconContext(
    GrovePanelMenuItem Item,
    string CssClass,
    string IconKey,
    string Title);
