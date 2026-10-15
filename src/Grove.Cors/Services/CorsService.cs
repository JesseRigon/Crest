using System.Text.Json.Nodes;
using Grove.Cors.Settings;
using Grove.Entities;
using Grove.Settings;

namespace Grove.Cors.Services;

public class CorsService
{
    private readonly ISiteService _siteService;

    public CorsService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<CorsSettings> GetSettingsAsync()
    {
        return await _siteService.GetSettingsAsync<CorsSettings>();
    }

    internal async Task UpdateSettingsAsync(CorsSettings corsSettings)
    {
        var siteSettings = await _siteService.LoadSiteSettingsAsync();
        siteSettings.Put(nameof(CorsSettings), corsSettings);
        await _siteService.UpdateSiteSettingsAsync(siteSettings);
    }
}
