using System.ComponentModel.DataAnnotations;
using Grove.Integrations.Facebook.Login.Settings;

namespace Grove.Integrations.Facebook.Login.Services;

public interface IFacebookLoginService
{
    Task<FacebookLoginSettings> GetSettingsAsync();
    Task<FacebookLoginSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(FacebookLoginSettings settings);
    Task<IEnumerable<ValidationResult>> ValidateSettingsAsync(FacebookLoginSettings settings);
}
