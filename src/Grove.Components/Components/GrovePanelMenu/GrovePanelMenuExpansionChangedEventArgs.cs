namespace Grove.Components.Navigation;

public sealed class GrovePanelMenuExpansionChangedEventArgs(GrovePanelMenuItem item, bool expanded)
{
    public GrovePanelMenuItem Item { get; } = item;

    public bool Expanded { get; } = expanded;
}
