#nullable enable

using Crest.ContentManagement;

namespace Crest.ContentTypes;

/// <summary>
/// Clears the fields of an item that its fields' visibility conditions hide
/// (<see cref="CrestFieldVisibilitySettings"/>), before the item is saved. Implemented by the host
/// beside the option picker, whose parent values the conditions read.
/// </summary>
public interface IFieldVisibilityEnforcer
{
    Task ClearHiddenFieldsAsync(ContentItem item, CancellationToken cancellationToken = default);
}
