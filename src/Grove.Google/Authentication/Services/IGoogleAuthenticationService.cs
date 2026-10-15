using System.ComponentModel.DataAnnotations;
using Grove.Google.Authentication.Settings;

namespace Grove.Google.Authentication.Services;

public interface IGoogleAuthenticationService
{
    Task<GoogleAuthenticationSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(GoogleAuthenticationSettings settings);

    IEnumerable<ValidationResult> ValidateSettings(GoogleAuthenticationSettings settings);
}
