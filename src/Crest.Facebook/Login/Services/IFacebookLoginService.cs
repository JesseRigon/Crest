using System.ComponentModel.DataAnnotations;
using Crest.Facebook.Login.Settings;

namespace Crest.Facebook.Login.Services;

public interface IFacebookLoginService
{
    Task<FacebookLoginSettings> GetSettingsAsync();
    Task<FacebookLoginSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(FacebookLoginSettings settings);
    Task<IEnumerable<ValidationResult>> ValidateSettingsAsync(FacebookLoginSettings settings);
}
