using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Configuration;

public sealed class AzureADSettingsConfiguration : IConfigureOptions<AzureADSettings>
{
    private readonly IAzureADService _azureADService;

    public AzureADSettingsConfiguration(IAzureADService azureADService)
    {
        _azureADService = azureADService;
    }

    public void Configure(AzureADSettings options)
    {
        var settings = GetAzureADSettingsAsync()
            .GetAwaiter()
            .GetResult();

        if (settings != null)
        {
            options.DisplayName = settings.DisplayName;
            options.CallbackPath = settings.CallbackPath;
            options.TenantId = settings.TenantId;
            options.SaveTokens = settings.SaveTokens;
        }
    }

    private async Task<AzureADSettings> GetAzureADSettingsAsync()
    {
        var settings = await _azureADService.GetSettingsAsync();

        if (_azureADService.ValidateSettings(settings).Any(result => result != ValidationResult.Success))
        {
            return null;
        }

        return settings;
    }
}
