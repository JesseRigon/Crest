namespace Grove.Contents.Types.Events;

/// <summary>
/// Defines methods for handling content definition events during the content type building process.
/// Implementations of this interface allow customization of content types, field sets, and fields 
/// before they are finalized in the content definition.
/// </summary>
public interface IContentDefinitionHandler
{
    /// <summary>
    /// Invoked during the building of a content type.
    /// Allows modifications or custom logic to be applied to the content type being created.
    /// </summary>
    /// <param name="context">The context for the content type being built.</param>
    void ContentTypeBuilding(ContentTypeBuildingContext context);

    /// <summary>
    /// Invoked during the building of a field set definition.
    /// Allows modification or customization of field sets before they are finalized in the content definition.
    /// </summary>
    /// <param name="context">The context for the field set definition being built.</param>
    void ContentFieldSetBuilding(ContentFieldSetBuildingContext context);

    /// <summary>
    /// Invoked during the building of a part on a content type.
    /// Enables modification or customization of the field set as it is attached to a content type.
    /// </summary>
    /// <param name="context">The context for the content type part being built.</param>
    void ContentTypeFieldSetBuilding(ContentTypeFieldSetBuildingContext context);

    /// <summary>
    /// Invoked during the building of a field on a field set.
    /// Allows customization of fields added to field sets before the final definition is created.
    /// </summary>
    /// <param name="context">The context for the field set field being built.</param>
    void ContentFieldBuilding(ContentFieldBuildingContext context);
}

