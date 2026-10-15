using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Represents the common <see cref="GroveChipList{TValue}" /> API used by
/// chip list items. Injected as a cascading property in <see cref="GroveChipItem" />.
/// </summary>
public interface IGroveChipList
{
    /// <summary>
    /// Adds the specified item to the chip list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void AddItem(GroveChipItem item);

    /// <summary>
    /// Removes the specified item from the chip list.
    /// </summary>
    /// <param name="item">The item.</param>
    void RemoveItem(GroveChipItem item);

    /// <summary>
    /// Refreshes this instance.
    /// </summary>
    void Refresh();
}
