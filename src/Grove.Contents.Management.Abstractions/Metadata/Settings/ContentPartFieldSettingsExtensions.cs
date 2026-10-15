using Grove.Contents.Management.Metadata.Builders;

namespace Grove.Contents.Management.Metadata.Settings;

public static class ContentPartFieldSettingsExtensions
{
    public static ContentPartFieldDefinitionBuilder WithDisplayName(this ContentPartFieldDefinitionBuilder builder, string displayName)
    {
        return builder.MergeSettings<ContentPartFieldSettings>(x => x.DisplayName = displayName);
    }

    public static ContentPartFieldDefinitionBuilder WithDescription(this ContentPartFieldDefinitionBuilder builder, string description)
    {
        return builder.MergeSettings<ContentPartFieldSettings>(x => x.Description = description);
    }

    public static ContentPartFieldDefinitionBuilder WithEditor(this ContentPartFieldDefinitionBuilder builder, string editor)
    {
        return builder.MergeSettings<ContentPartFieldSettings>(x => x.Editor = editor);
    }

    public static ContentPartFieldDefinitionBuilder WithDisplayMode(this ContentPartFieldDefinitionBuilder builder, string displayMode)
    {
        return builder.MergeSettings<ContentPartFieldSettings>(x => x.DisplayMode = displayMode);
    }

    public static ContentPartFieldDefinitionBuilder WithPosition(this ContentPartFieldDefinitionBuilder builder, string position)
    {
        return builder.MergeSettings<ContentPartFieldSettings>(x => x.Position = position);
    }
}
