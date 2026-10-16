using Grove.Contents.Management.Metadata.Builders;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Utilities;

namespace Grove.Contents.Management.Metadata.Settings;

public static class ContentFieldSetSettingsExtensions
{
    public static ContentFieldSetDefinitionBuilder Attachable(this ContentFieldSetDefinitionBuilder builder, bool attachable = true)
    {
        return builder.MergeSettings<ContentFieldSetSettings>(x => x.Attachable = attachable);
    }

    public static bool IsAttachable(this ContentFieldSetDefinition part)
    {
        return part.GetSettings<ContentFieldSetSettings>().Attachable;
    }

    public static ContentFieldSetDefinitionBuilder Reusable(this ContentFieldSetDefinitionBuilder builder, bool reusable = true)
    {
        return builder.MergeSettings<ContentFieldSetSettings>(x => x.Reusable = reusable);
    }

    public static bool IsReusable(this ContentFieldSetDefinition part)
    {
        return part.GetSettings<ContentFieldSetSettings>().Reusable;
    }

    public static ContentFieldSetDefinitionBuilder WithDescription(this ContentFieldSetDefinitionBuilder builder, string description)
    {
        return builder.MergeSettings<ContentFieldSetSettings>(x => x.Description = description);
    }

    public static ContentFieldSetDefinitionBuilder WithDisplayName(this ContentFieldSetDefinitionBuilder builder, string description)
    {
        return builder.MergeSettings<ContentFieldSetSettings>(x => x.DisplayName = description);
    }

    public static ContentFieldSetDefinitionBuilder WithDefaultPosition(this ContentFieldSetDefinitionBuilder builder, string position)
    {
        return builder.MergeSettings<ContentFieldSetSettings>(x => x.DefaultPosition = position);
    }

    public static string DefaultPosition(this ContentFieldSetDefinition part)
    {
        return part.GetSettings<ContentFieldSetSettings>().DefaultPosition;
    }

    public static string Description(this ContentFieldSetDefinition part)
    {
        return part.GetSettings<ContentFieldSetSettings>().Description;
    }

    public static string DisplayName(this ContentFieldSetDefinition part)
    {
        var displayName = part.GetSettings<ContentFieldSetSettings>().DisplayName;

        if (string.IsNullOrEmpty(displayName))
        {
            displayName = part.Name.TrimEndString("Part");
        }

        return displayName;
    }
}
