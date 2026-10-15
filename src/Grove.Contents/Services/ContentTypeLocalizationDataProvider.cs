using Grove.Contents.Management.Metadata;
using Grove.Contents.Types;
using Grove.Data.Localization.Data;

namespace Grove.Contents.Services;

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
