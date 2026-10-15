using System.ComponentModel.DataAnnotations;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Services;

public interface IAzureADService
{
    Task<AzureADSettings> GetSettingsAsync();
    Task<AzureADSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(AzureADSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(AzureADSettings settings);
}
