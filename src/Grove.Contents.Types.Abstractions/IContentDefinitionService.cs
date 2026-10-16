using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Types;

/// <summary>
/// Provides a reusable service for content definition operations.
/// This interface provides methods that trigger content definition events and can be used across modules.
/// </summary>
public interface IContentDefinitionService
{
    /// <summary>
    /// Creates a new content type with the specified name and display name.
    /// </summary>
    /// <param name="name">The technical name of the content type. If null or whitespace, it will be generated from the display name.</param>
    /// <param name="displayName">The display name of the content type.</param>
    /// <returns>The created content type definition.</returns>
    Task<ContentTypeDefinition> AddTypeAsync(string name, string displayName);

    /// <summary>
    /// Removes a content type.
    /// </summary>
    /// <param name="name">The technical name of the content type to remove.</param>
    /// <param name="deleteContent">Whether to delete the associated content items.</param>
    Task RemoveTypeAsync(string name, bool deleteContent);

    /// <summary>
    /// Adds a part to a content type.
    /// </summary>
    /// <param name="partName">The name of the part to add.</param>
    /// <param name="typeName">The name of the content type to add the part to.</param>
    Task AddFieldSetToTypeAsync(string fieldSetName, string typeName);

    /// <summary>
    /// Adds a reusable part to a content type with a custom name.
    /// </summary>
    /// <param name="name">The custom name for the part instance.</param>
    /// <param name="displayName">The display name for the part instance.</param>
    /// <param name="description">The description for the part instance.</param>
    /// <param name="partName">The name of the part definition to add.</param>
    /// <param name="typeName">The name of the content type to add the part to.</param>
    Task AddReusableFieldSetToTypeAsync(string name, string displayName, string description, string fieldSetName, string typeName);

    /// <summary>
    /// Removes a part from a content type.
    /// </summary>
    /// <param name="partName">The name of the part to remove.</param>
    /// <param name="typeName">The name of the content type to remove the part from.</param>
    Task RemoveFieldSetFromTypeAsync(string fieldSetName, string typeName);

    /// <summary>
    /// Creates a new field set definition.
    /// </summary>
    /// <param name="name">The technical name of the part to create.</param>
    /// <returns>The created field set definition.</returns>
    Task<ContentFieldSetDefinition> AddFieldSetAsync(string name);

    /// <summary>
    /// Removes a field set definition.
    /// </summary>
    /// <param name="name">The technical name of the part to remove.</param>
    Task RemoveFieldSetAsync(string name);

    /// <summary>
    /// Adds a field to a field set.
    /// </summary>
    /// <param name="fieldName">The technical name of the field.</param>
    /// <param name="fieldTypeName">The type name of the field.</param>
    /// <param name="partName">The name of the part to add the field to.</param>
    Task AddFieldToFieldSetAsync(string fieldName, string fieldTypeName, string fieldSetName);

    /// <summary>
    /// Adds a field to a field set with a display name.
    /// </summary>
    /// <param name="fieldName">The technical name of the field.</param>
    /// <param name="displayName">The display name of the field.</param>
    /// <param name="fieldTypeName">The type name of the field.</param>
    /// <param name="partName">The name of the part to add the field to.</param>
    Task AddFieldToFieldSetAsync(string fieldName, string displayName, string fieldTypeName, string fieldSetName);

    /// <summary>
    /// Removes a field from a field set.
    /// </summary>
    /// <param name="fieldName">The name of the field to remove.</param>
    /// <param name="partName">The name of the part to remove the field from.</param>
    Task RemoveFieldFromFieldSetAsync(string fieldName, string fieldSetName);

    /// <summary>
    /// Alters a field's settings in a field set.
    /// </summary>
    /// <param name="context">The context containing field alteration details.</param>
    Task AlterFieldAsync(AlterFieldContext context);

    /// <summary>
    /// Alters a type part's settings in a content type.
    /// </summary>
    /// <param name="context">The context containing type part alteration details.</param>
    Task AlterTypeFieldSetAsync(AlterTypeFieldSetContext context);

    /// <summary>
    /// Alters the order of parts in a content type.
    /// </summary>
    /// <param name="typeDefinition">The content type definition to alter.</param>
    /// <param name="partNames">The ordered array of part names.</param>
    Task AlterTypeFieldSetsOrderAsync(ContentTypeDefinition typeDefinition, string[] fieldSetNames);

    /// <summary>
    /// Alters the order of fields in a field set.
    /// </summary>
    /// <param name="partDefinition">The field set definition to alter.</param>
    /// <param name="fieldNames">The ordered array of field names.</param>
    Task AlterFieldSetFieldsOrderAsync(ContentFieldSetDefinition fieldSetDefinition, string[] fieldNames);

    /// <summary>
    /// Generates a unique content type name from a display name.
    /// </summary>
    /// <param name="displayName">The display name to generate from.</param>
    /// <returns>A unique content type name.</returns>
    Task<string> GenerateContentTypeNameFromDisplayNameAsync(string displayName);

    /// <summary>
    /// Generates a unique field name from a display name within a part.
    /// </summary>
    /// <param name="partName">The name of the part containing the field.</param>
    /// <param name="displayName">The display name to generate from.</param>
    /// <returns>A unique field name.</returns>
    Task<string> GenerateFieldNameFromDisplayNameAsync(string fieldSetName, string displayName);
}
