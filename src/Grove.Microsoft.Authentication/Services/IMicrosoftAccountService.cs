using System.ComponentModel.DataAnnotations;
using Grove.Microsoft.Authentication.Settings;

namespace Grove.Microsoft.Authentication.Services;

public interface IMicrosoftAccountService
{
    Task<MicrosoftAccountSettings> GetSettingsAsync();
    Task<MicrosoftAccountSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(MicrosoftAccountSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(MicrosoftAccountSettings settings);
}
