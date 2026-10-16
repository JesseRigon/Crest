#nullable enable

namespace Grove.Contents.Types;

/// <summary>
/// Turns the values a field stores into the values a field condition compares. A scalar field
/// stores what conditions compare; a field that stores references (the option picker's ids)
/// resolves them to the referenced records' keys. The option picker registers one; without it the
/// stored values are compared as they are.
/// </summary>
public interface IFieldComparisonValueResolver
{
    /// <summary>
    /// The comparison values <paramref name="rawValues"/> stand for. <paramref name="path"/> is
    /// "FieldSet.Field" (by the type's attachment name); <paramref name="column"/> names the
    /// referenced record's column to compare (null for its key).
    /// </summary>
    Task<FieldComparisonValues> ResolveAsync(string contentType, string path, string? column, IReadOnlyList<string> rawValues, CancellationToken cancellationToken = default);
}

/// <summary>
/// A field's comparison values. <see cref="IgnoreCase"/> values (option keys) are lower-cased, and
/// the literals a condition compares them with are lower-cased too (<see cref="Grove.Display.Management.Components.FieldConditions.IgnoreCase"/>).
/// </summary>
public sealed record FieldComparisonValues(IReadOnlyList<string> Values, bool IgnoreCase = false);
