using Microsoft.AspNetCore.Components.Web;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about GroveDropZoneContainer CanDrop function and GroveDropZone Drop event.
/// </summary>
public class GroveDropZoneItemEventArgs<TItem>
{
    /// <summary>
    /// Gets the dragged item zone.
    /// </summary>
    public GroveDropZone<TItem>? FromZone { get; internal set; }

    /// <summary>
    /// Gets the drop zone.
    /// </summary>
    public GroveDropZone<TItem>? ToZone { get; internal set; }

    /// <summary>
    /// Gets the dragged item.
    /// </summary>
    public TItem? Item { get; internal set; }

    /// <summary>
    /// Gets the dropped item.
    /// </summary>
    public TItem? ToItem { get; internal set; }

    /// <summary>
    /// The data that underlies a drag-and-drop operation, known as the drag data store.
    /// See <see cref="DataTransfer"/>.
    /// </summary>
    public DataTransfer DataTransfer { get; set; } = default!;
}

