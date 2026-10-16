using Grove.Contents.Management.Metadata.Builders;

namespace Grove.Contents.Management.Metadata.Settings;

public static class ContentFieldSettingsExtensions
{
    public static ContentFieldDefinitionBuilder WithDisplayName(this ContentFieldDefinitionBuilder builder, string displayName)
    {
        return builder.MergeSettings<ContentFieldSettings>(x => x.DisplayName = displayName);
    }

    public static ContentFieldDefinitionBuilder WithDescription(this ContentFieldDefinitionBuilder builder, string description)
    {
        return builder.MergeSettings<ContentFieldSettings>(x => x.Description = description);
    }

    public static ContentFieldDefinitionBuilder WithEditor(this ContentFieldDefinitionBuilder builder, string editor)
    {
        return builder.MergeSettings<ContentFieldSettings>(x => x.Editor = editor);
    }

    public static ContentFieldDefinitionBuilder WithDisplayMode(this ContentFieldDefinitionBuilder builder, string displayMode)
    {
        return builder.MergeSettings<ContentFieldSettings>(x => x.DisplayMode = displayMode);
    }

    public static ContentFieldDefinitionBuilder WithPosition(this ContentFieldDefinitionBuilder builder, string position)
    {
        return builder.MergeSettings<ContentFieldSettings>(x => x.Position = position);
    }
}

/// <summary>Editor modes every field type understands.</summary>
/// <summary>The display modes a field can be given beyond its type's own.</summary>
public static class ContentFieldDisplayModes
{
    /// <summary>The field has no display of its own: its field set's display shows what it means (a title, a permalink), or it is data a module keeps.</summary>
    public const string None = "None";
}

public static class ContentFieldEditors
{
    /// <summary>The field has no editor of its own: its field set's editor shows it, or its system flows keep it.</summary>
    public const string None = "None";
}
