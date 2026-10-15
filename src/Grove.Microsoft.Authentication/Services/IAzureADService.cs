using System.ComponentModel.DataAnnotations;
using Grove.Microsoft.Authentication.Settings;

namespace Grove.Microsoft.Authentication.Services;

public interface IAzureADService
{
    Task<AzureADSettings> GetSettingsAsync();
    Task<AzureADSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(AzureADSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(AzureADSettings settings);
}
