using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Management.Metadata.Models;

public static class ContentTypeFieldSetExtensions
{
    public static string DisplayName(this ContentTypeFieldSetDefinition typeFieldSet)
    {
        var displayName = typeFieldSet.GetSettings<ContentTypeFieldSetSettings>().DisplayName;

        if (string.IsNullOrEmpty(displayName))
        {
            // ContentType creates a same named ContentFieldSet. As DisplayName is not stored in ContentFieldSet,
            // fetching it from the parent ContentType
            if (typeFieldSet.FieldSetDefinition.Name == typeFieldSet.ContentTypeDefinition.Name)
            {
                displayName = typeFieldSet.ContentTypeDefinition.DisplayName;
            }
            else
            {
                displayName = typeFieldSet.FieldSetDefinition.DisplayName();
            }
        }

        return displayName;
    }

    public static string Description(this ContentTypeFieldSetDefinition typeFieldSet)
    {
        var description = typeFieldSet.GetSettings<ContentTypeFieldSetSettings>().Description;

        if (string.IsNullOrEmpty(description))
        {
            description = typeFieldSet.FieldSetDefinition.Description();
        }

        return description;
    }

    public static string Editor(this ContentTypeFieldSetDefinition typeFieldSet)
    {
        return typeFieldSet.GetSettings<ContentTypeFieldSetSettings>().Editor;
    }

    public static string DisplayMode(this ContentTypeFieldSetDefinition typeFieldSet)
    {
        return typeFieldSet.GetSettings<ContentTypeFieldSetSettings>().DisplayMode;
    }

    public static bool IsNamedFieldSet(this ContentTypeFieldSetDefinition typeFieldSet)
        => typeFieldSet.FieldSetDefinition.IsReusable() && typeFieldSet.Name != typeFieldSet.FieldSetDefinition.Name;
}
