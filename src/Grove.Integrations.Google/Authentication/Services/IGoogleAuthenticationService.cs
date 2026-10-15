using System.ComponentModel.DataAnnotations;
using Grove.Integrations.Google.Authentication.Settings;

namespace Grove.Integrations.Google.Authentication.Services;

public interface IGoogleAuthenticationService
{
    Task<GoogleAuthenticationSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(GoogleAuthenticationSettings settings);

    IEnumerable<ValidationResult> ValidateSettings(GoogleAuthenticationSettings settings);
}
