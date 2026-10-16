using Grove.Display.Management;

namespace Grove.Contents.Management.Display;

/// <summary>
/// Describes services responsible for displaying a content item. The result dynamic objects
/// are the Shape to render a <see cref="ContentItem"/>.
/// </summary>
public interface IContentItemDisplayManager
{
    Task<IShape> BuildDisplayAsync(ContentItem content, string displayType = "", string groupId = "");

    /// <summary>
    /// The display of <paramref name="content"/> read through <paramref name="definition"/>, not its
    /// type's current definition: a frozen embedded item displays through the schema stored with it
    /// (<see cref="IEmbeddedContentReader"/>). The definition travels on the shape
    /// (<see cref="ContentTypeDefinitionProperty"/>) to every driver that reads it.
    /// </summary>
    Task<IShape> BuildDisplayAsync(ContentItem content, Metadata.Models.ContentTypeDefinition definition, string displayType = "", string groupId = "");

    /// <summary>The shape property holding the definition a display is built with.</summary>
    public const string ContentTypeDefinitionProperty = "ContentTypeDefinition";
}

public static class ContentDisplayDefinitions
{
    /// <summary>The definition the display of the shape's item is built with: the one it was given, else the type's current one.</summary>
    public static async Task<Metadata.Models.ContentTypeDefinition> DefinitionAsync(IShape shape, ContentItem contentItem, Metadata.IContentDefinitionManager definitions) =>
        shape?.Properties.TryGetValue(IContentItemDisplayManager.ContentTypeDefinitionProperty, out var given) == true && given is Metadata.Models.ContentTypeDefinition definition
            ? definition
            : await definitions.GetTypeDefinitionAsync(contentItem.ContentType);
}
