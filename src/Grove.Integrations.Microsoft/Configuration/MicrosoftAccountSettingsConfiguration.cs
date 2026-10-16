using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Configuration;

public sealed class MicrosoftAccountSettingsConfiguration : IConfigureOptions<MicrosoftAccountSettings>
{
    private readonly IMicrosoftAccountService _microsoftAccountService;

    public MicrosoftAccountSettingsConfiguration(IMicrosoftAccountService microsoftAccountService)
    {
        _microsoftAccountService = microsoftAccountService;
    }

    public void Configure(MicrosoftAccountSettings options)
    {
        var settings = GetMicrosoftAccountSettingsAsync()
            .GetAwaiter()
            .GetResult();

        if (settings != null)
        {
            options.CallbackPath = settings.CallbackPath;
            options.SaveTokens = settings.SaveTokens;
        }
    }

    private async Task<MicrosoftAccountSettings> GetMicrosoftAccountSettingsAsync()
    {
        var settings = await _microsoftAccountService.GetSettingsAsync();
        if (_microsoftAccountService.ValidateSettings(settings).Any(result => result != ValidationResult.Success))
        {
            return null;
        }

        return settings;
    }
}
