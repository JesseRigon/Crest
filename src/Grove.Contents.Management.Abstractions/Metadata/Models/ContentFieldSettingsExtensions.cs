using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Management.Metadata.Models;

public static class ContentFieldSettingsExtensions
{
    public static string DisplayName(this ContentFieldDefinition partField)
    {
        var displayName = partField.GetSettings<ContentFieldSettings>().DisplayName;

        if (string.IsNullOrEmpty(displayName))
        {
            displayName = partField.FieldTypeDefinition?.Name;
        }

        return displayName;
    }

    public static string Description(this ContentFieldDefinition partField)
    {
        return partField.GetSettings<ContentFieldSettings>().Description;
    }

    public static string Editor(this ContentFieldDefinition partField)
    {
        return partField.GetSettings<ContentFieldSettings>().Editor;
    }

    public static string DisplayMode(this ContentFieldDefinition partField)
    {
        return partField.GetSettings<ContentFieldSettings>().DisplayMode;
    }
}
