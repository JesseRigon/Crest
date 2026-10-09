using System.ComponentModel.DataAnnotations;
using Crest.Google.Authentication.Settings;

namespace Crest.Google.Authentication.Services;

public interface IGoogleAuthenticationService
{
    Task<GoogleAuthenticationSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(GoogleAuthenticationSettings settings);

    IEnumerable<ValidationResult> ValidateSettings(GoogleAuthenticationSettings settings);
}
