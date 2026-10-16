using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Grove.Entities;
using Grove.Integrations.Microsoft.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Microsoft.Services;

public class MicrosoftAccountService : IMicrosoftAccountService
{
    private readonly ISiteService _siteService;
    protected readonly IStringLocalizer S;

    public MicrosoftAccountService(
        ISiteService siteService,
        IStringLocalizer<MicrosoftAccountService> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public Task<MicrosoftAccountSettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<MicrosoftAccountSettings>();

    public async Task<MicrosoftAccountSettings> LoadSettingsAsync()
    {
        var container = await _siteService.LoadSiteSettingsAsync();
        return container.GetOrCreate<MicrosoftAccountSettings>();
    }

    public async Task UpdateSettingsAsync(MicrosoftAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var container = await _siteService.LoadSiteSettingsAsync();
        container.Alter<MicrosoftAccountSettings>(aspect =>
        {
            aspect.SaveTokens = settings.SaveTokens;
            aspect.CallbackPath = settings.CallbackPath;
        });

        await _siteService.UpdateSiteSettingsAsync(container);
    }

    public IEnumerable<ValidationResult> ValidateSettings(MicrosoftAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        yield break;
    }
}
