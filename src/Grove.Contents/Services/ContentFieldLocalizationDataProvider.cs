using Grove.Contents.Fields;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Localization.Data;

namespace Grove.Contents.Services;

public class ContentFieldDataLocalizationProvider : ILocalizationDataProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContentFieldDataLocalizationProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var typeDefinitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        // "Content Fields" is the primary context and the field set's attachment name on the
        // type the sub-context: "Content Fields:{FieldSetAttachment}". A field's label and its
        // description are both sources.
        return typeDefinitions
            .SelectMany(t => t.FieldSets)
            .SelectMany(p => p.FieldSetDefinition.Fields.SelectMany(f => new[] { f.DisplayName(), f.Description() }
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => new DataLocalizedString(DataLocalizationContext.ContentField(p.Name), text, string.Empty))))
            .DistinctBy(d => $"{d.Context}|{d.Name}");
    }
}
