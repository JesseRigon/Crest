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

        // Use "Content Fields" as primary context and part name as sub-context for grouping.
        // Format: "Content Fields:PartName" where PartName is the content part name.
        return typeDefinitions
            .SelectMany(t => t.Parts)
            .SelectMany(p => p.PartDefinition.Fields.Select(f =>
                new DataLocalizedString(
                    DataLocalizationContext.ContentField(p.PartDefinition.Name),
                    f.DisplayName(),
                    string.Empty)))
            .DistinctBy(d => $"{d.Context}|{d.Name}");
    }
}
