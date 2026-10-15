using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Model;

public sealed class GroveModelListAction<TItem>
{
    public string Icon { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ButtonStyle ButtonStyle { get; init; } = ButtonStyle.Light;
    public EventCallback<TItem> Callback { get; init; }
    public Func<TItem, Task>? Handler { get; init; }
    public Func<TItem, bool>? Disabled { get; init; }

    public Task InvokeAsync(TItem item) => Callback.HasDelegate
        ? Callback.InvokeAsync(item)
        : Handler?.Invoke(item) ?? Task.CompletedTask;
}
