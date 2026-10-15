using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Represents the common <see cref="GroveSelectBar{TValue}" /> API used by
/// its items. Injected as a cascading property in <see cref="GroveSelectBarItem" />.
/// </summary>
public interface IGroveSelectBar
{
    /// <summary>
    /// Adds the specified item to the select bar.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void AddItem(GroveSelectBarItem item);

    /// <summary>
    /// Removes the specified item from the select bar.
    /// </summary>
    /// <param name="item">The item.</param>
    void RemoveItem(GroveSelectBarItem item);

    /// <summary>
    /// Refreshes this instance.
    /// </summary>
    void Refresh();
}

