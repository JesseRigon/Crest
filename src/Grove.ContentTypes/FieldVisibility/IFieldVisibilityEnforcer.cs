#nullable enable

using Grove.ContentManagement;

namespace Grove.ContentTypes;

/// <summary>
/// Clears the fields of an item that its fields' visibility conditions hide
/// (<see cref="GroveFieldVisibilitySettings"/>), before the item is saved.
/// </summary>
public interface IFieldVisibilityEnforcer
{
    Task ClearHiddenFieldsAsync(ContentItem item, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns the values a field stores into the values a visibility condition compares. A scalar
/// field stores what conditions compare; a field that stores references (the option picker's
/// ids) resolves them to the referenced records' keys. The option picker registers one; without
/// it the stored values are compared as they are.
/// </summary>
public interface IFieldComparisonValueResolver
{
    /// <summary>
    /// The comparison values <paramref name="rawValues"/> stand for. <paramref name="path"/> is
    /// "Field" (a field of the type's implicit part) or "Part.Field"; <paramref name="column"/>
    /// names the referenced record's column to compare (null for its key).
    /// </summary>
    Task<IReadOnlyList<string>> ResolveAsync(string contentType, string path, string? column, IReadOnlyList<string> rawValues, CancellationToken cancellationToken = default);
}
