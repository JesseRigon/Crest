#nullable enable

using System.Text.Json.Serialization;
using Grove.Display.Management.Components;

namespace Grove.Contents.Types;

/// <summary>
/// A visibility condition on any field the editor renders: the field shows only while
/// <see cref="When"/> holds ("Void reason appears when <c>Invoice.Status == "Voided"</c>"). A field
/// setting, not a field type's setting: the dependent field can be of any type.
/// </summary>
/// <remarks>
/// <see cref="When"/> is a field condition (<see cref="FieldConditions"/>): a predicate over field
/// paths (<c>FieldSet.Field</c>) and literals, evaluated by the one evaluator the editor and the
/// save share. A field compares its comparison values (<see cref="IFieldComparisonValueResolver"/>):
/// an option list field its option keys, never labels or per-tenant ids; a scalar field its stored
/// value. A hidden field's value is cleared when the item is saved, so a document never keeps a
/// stale dependent value (an un-voided invoice keeping its void reason).
/// </remarks>
public class FieldVisibilitySettings
{
    /// <summary>The condition the field shows under; none means always visible.</summary>
    public DisplayBinding? When { get; set; }

    [JsonIgnore]
    public bool HasCondition => When is not null;
}
