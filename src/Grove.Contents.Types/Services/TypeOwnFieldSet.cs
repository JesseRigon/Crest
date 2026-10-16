using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;

namespace Grove.Contents.Types.Services;

/// <summary>
/// A type's own field set is the field set named after the type. It can be a field set defined
/// in code (the <c>Taxonomy</c> type's own set is <c>TaxonomyFieldSet</c>), and it can be shared:
/// other types may attach it too. Removing a type removes its own set's definition only when
/// neither holds.
/// </summary>
internal static class TypeOwnFieldSet
{
    public static async Task<bool> CanDeleteAsync(IContentDefinitionManager definitions, ContentOptions contentOptions, string typeName)
    {
        if (contentOptions.ContentFieldSetOptionsLookup.ContainsKey(typeName))
        {
            return false;
        }

        var types = await definitions.LoadTypeDefinitionsAsync();

        return !types.Any(type =>
            !string.Equals(type.Name, typeName, StringComparison.Ordinal) &&
            type.FieldSets.Any(fieldSet => string.Equals(fieldSet.FieldSetDefinition.Name, typeName, StringComparison.Ordinal)));
    }
}
