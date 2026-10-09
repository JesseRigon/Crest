using System.ComponentModel.DataAnnotations;
using Crest.Microsoft.Authentication.Settings;

namespace Crest.Microsoft.Authentication.Services;

public interface IAzureADService
{
    Task<AzureADSettings> GetSettingsAsync();
    Task<AzureADSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(AzureADSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(AzureADSettings settings);
}
