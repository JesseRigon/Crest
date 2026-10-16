using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Metadata;

/// <summary>
/// This interface provides each client with
/// a different copy of <see cref="ContentTypeDefinition"/> to work with in case
/// multiple clients do modifications.
/// </summary>
public interface IContentDefinitionManager
{
    Task<IEnumerable<ContentTypeDefinition>> LoadTypeDefinitionsAsync();

    Task<IEnumerable<ContentTypeDefinition>> ListTypeDefinitionsAsync();

    Task<IEnumerable<ContentFieldSetDefinition>> LoadFieldSetDefinitionsAsync();

    Task<IEnumerable<ContentFieldSetDefinition>> ListFieldSetDefinitionsAsync();

    Task<ContentTypeDefinition> LoadTypeDefinitionAsync(string name);

    Task<ContentTypeDefinition> GetTypeDefinitionAsync(string name);

    Task<ContentFieldSetDefinition> LoadFieldSetDefinitionAsync(string name);

    Task<ContentFieldSetDefinition> GetFieldSetDefinitionAsync(string name);

    Task DeleteTypeDefinitionAsync(string name);

    Task DeleteFieldSetDefinitionAsync(string name);

    Task StoreTypeDefinitionAsync(ContentTypeDefinition contentTypeDefinition);

    Task StoreFieldSetDefinitionAsync(ContentFieldSetDefinition contentFieldSetDefinition);

    /// <summary>
    /// Returns an unique identifier that is updated when content definitions have changed.
    /// </summary>
    Task<string> GetIdentifierAsync();
}
