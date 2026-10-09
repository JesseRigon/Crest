using System.ComponentModel.DataAnnotations;
using Crest.Microsoft.Authentication.Settings;

namespace Crest.Microsoft.Authentication.Services;

public interface IMicrosoftAccountService
{
    Task<MicrosoftAccountSettings> GetSettingsAsync();
    Task<MicrosoftAccountSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(MicrosoftAccountSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(MicrosoftAccountSettings settings);
}
