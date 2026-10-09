using Crest.ContentManagement.Metadata;
using Crest.ContentTypes;
using Crest.Localization.Data;

namespace Crest.Contents.Services;

public class ContentTypeDataLocalizationProvider : ILocalizationDataProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContentTypeDataLocalizationProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Select(t => new DataLocalizedString(DataLocalizationContext.ContentType, t.DisplayName, string.Empty));
}
