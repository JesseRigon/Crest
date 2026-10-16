using Grove.Contents.Management.Metadata.Builders;

namespace Grove.Contents.Management.Metadata.Settings;

public static class ContentTypeFieldSetSettingsExtensions
{
    public static ContentTypeFieldSetDefinitionBuilder WithDisplayName(this ContentTypeFieldSetDefinitionBuilder builder, string displayName)
    {
        return builder.MergeSettings<ContentTypeFieldSetSettings>(x => x.DisplayName = displayName);
    }

    public static ContentTypeFieldSetDefinitionBuilder WithDescription(this ContentTypeFieldSetDefinitionBuilder builder, string description)
    {
        return builder.MergeSettings<ContentTypeFieldSetSettings>(x => x.Description = description);
    }

    public static ContentTypeFieldSetDefinitionBuilder WithPosition(this ContentTypeFieldSetDefinitionBuilder builder, string position)
    {
        return builder.MergeSettings<ContentTypeFieldSetSettings>(x => x.Position = position);
    }

    public static ContentTypeFieldSetDefinitionBuilder WithDisplayMode(this ContentTypeFieldSetDefinitionBuilder builder, string displayMode)
    {
        return builder.MergeSettings<ContentTypeFieldSetSettings>(x => x.DisplayMode = displayMode);
    }

    public static ContentTypeFieldSetDefinitionBuilder WithEditor(this ContentTypeFieldSetDefinitionBuilder builder, string editor)
    {
        return builder.MergeSettings<ContentTypeFieldSetSettings>(x => x.Editor = editor);
    }
}
