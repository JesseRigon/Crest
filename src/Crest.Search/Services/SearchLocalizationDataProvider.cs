using Crest.Localization.Data;
using Crest.Search.Models;
using Crest.Settings;

namespace Crest.Search.Services;

internal sealed class SearchLocalizationDataProvider : ILocalizationDataProvider
{
    private readonly ISiteService _siteService;

    public SearchLocalizationDataProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var siteSettings = await _siteService.GetSiteSettingsAsync();
        if (!siteSettings.TryGet<SearchSettings>(out var searchSettings) ||
            string.IsNullOrEmpty(searchSettings.Placeholder))
        {
            return Enumerable.Empty<DataLocalizedString>();
        }

        return new[]
        {
            new DataLocalizedString(DataLocalizationContext.Search, searchSettings.Placeholder, string.Empty),
        };
    }
}
