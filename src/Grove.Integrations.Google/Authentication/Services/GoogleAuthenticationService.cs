using Grove.Entities;
using Grove.Integrations.Google.Authentication.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Google.Authentication.Services;

public class GoogleAuthenticationService : IGoogleAuthenticationService
{
    private readonly ISiteService _siteService;

    public GoogleAuthenticationService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<GoogleAuthenticationSettings> GetSettingsAsync()
    {
        var container = await _siteService.GetSiteSettingsAsync();
        return container.GetOrCreate<GoogleAuthenticationSettings>();
    }

    public async Task<GoogleAuthenticationSettings> LoadSettingsAsync()
    {
        var container = await _siteService.LoadSiteSettingsAsync();
        return container.GetOrCreate<GoogleAuthenticationSettings>();
    }

    public async Task UpdateSettingsAsync(GoogleAuthenticationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var container = await _siteService.LoadSiteSettingsAsync();
        container.Alter<GoogleAuthenticationSettings>(aspect =>
        {
            aspect.CallbackPath = settings.CallbackPath;
            aspect.SaveTokens = settings.SaveTokens;
        });

        await _siteService.UpdateSiteSettingsAsync(container);
    }
}
