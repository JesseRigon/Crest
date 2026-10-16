using Grove.Contents.Management.Metadata.Builders;
using Grove.Models;

namespace Grove.Infrastructure.Settings;

/// <summary>
/// Declaring and reading a content item pick: an option picker field over the content items
/// source (docs/server.md § 3), the one field type for picking.
/// </summary>
public static class ContentItemPickerSettings
{
    public const string FieldType = "OptionListField";

    /// <summary>Makes the field an option picker over content items of <paramref name="types"/>.</summary>
    public static ContentFieldDefinitionBuilder AsContentItemPicker(
        this ContentFieldDefinitionBuilder field,
        IEnumerable<string> types,
        bool multiple = false,
        bool required = false,
        Action<OptionListFieldSettings>? configure = null) =>
        field
            .OfType(FieldType)
            .MergeSettings<OptionListFieldSettings>(settings =>
            {
                settings.SourceKey = OptionSourceKeys.ForContentItems(types);
                settings.Multiple = multiple;
                settings.Required = required;
                configure?.Invoke(settings);
            });

    /// <summary>The content types an option picker over content items names (empty for another
    /// source, or for one over stereotypes or all types).</summary>
    public static IReadOnlyList<string> ContentTypes(this OptionListFieldSettings settings)
    {
        var (provider, qualifier) = OptionSourceKeys.Split(settings.SourceKey);
        return provider == OptionSourceKeys.ContentItemProvider ? ContentItemSourceScope.Parse(qualifier).Types : [];
    }
}
