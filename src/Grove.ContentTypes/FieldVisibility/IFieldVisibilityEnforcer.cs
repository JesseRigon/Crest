#nullable enable

using Grove.ContentManagement;

namespace Grove.ContentTypes;

/// <summary>
/// Clears the fields of an item that its fields' visibility conditions hide
/// (<see cref="GroveFieldVisibilitySettings"/>), before the item is saved. Implemented by the host
/// beside the option picker, whose parent values the conditions read.
/// </summary>
public interface IFieldVisibilityEnforcer
{
    Task ClearHiddenFieldsAsync(ContentItem item, CancellationToken cancellationToken = default);
}
