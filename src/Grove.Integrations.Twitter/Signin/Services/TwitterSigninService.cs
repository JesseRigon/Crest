using Grove.Entities;
using Grove.Infrastructure.Settings;
using Grove.Integrations.Twitter.Signin.Settings;

namespace Grove.Integrations.Twitter.Signin.Services;

public class TwitterSigninService : ITwitterSigninService
{
    private readonly ISiteService _siteService;

    public TwitterSigninService(
        ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<TwitterSigninSettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<TwitterSigninSettings>();

    public async Task<TwitterSigninSettings> LoadSettingsAsync()
    {
        var container = await _siteService.LoadSiteSettingsAsync();
        return container.GetOrCreate<TwitterSigninSettings>();
    }

    public async Task UpdateSettingsAsync(TwitterSigninSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var container = await _siteService.LoadSiteSettingsAsync();
        container.Alter<TwitterSigninSettings>(aspect =>
        {
            aspect.CallbackPath = settings.CallbackPath;
        });

        await _siteService.UpdateSiteSettingsAsync(container);
    }
}
