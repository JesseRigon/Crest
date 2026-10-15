using System.ComponentModel.DataAnnotations;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Services;

public interface IMicrosoftAccountService
{
    Task<MicrosoftAccountSettings> GetSettingsAsync();
    Task<MicrosoftAccountSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(MicrosoftAccountSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(MicrosoftAccountSettings settings);
}
