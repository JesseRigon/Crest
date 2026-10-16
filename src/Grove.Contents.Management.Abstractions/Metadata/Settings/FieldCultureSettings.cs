using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Metadata.Settings;

/// <summary>
/// Whether culture affects each field of a field-set attachment (docs/content-engine.md ›
/// Localization): a field the culture affects (the default) displays its amounts, quantities and
/// dates formatted for the reader's culture; one listed here displays them the same in every
/// culture. Set on the attachment, not the field, because a shared field may be culture-dependent
/// on one type and not on another.
/// </summary>
public sealed class FieldCultureSettings
{
    /// <summary>The fields of the attachment that culture does not affect.</summary>
    public string[] InvariantFields { get; set; } = [];
}

public static class FieldCultureSettingsExtensions
{
    /// <summary>Whether culture affects the field where its field set is attached (true unless the attachment lists it as invariant).</summary>
    public static bool IsCultureAffected(this ContentFieldDefinition field) =>
        field.ContentTypeFieldSetDefinition?.GetSettings<FieldCultureSettings>().InvariantFields?.Contains(field.Name, StringComparer.OrdinalIgnoreCase) != true;
}
