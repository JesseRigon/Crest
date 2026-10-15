using System.ComponentModel.DataAnnotations;
using Grove.Facebook.Settings;

namespace Grove.Facebook.Services;

public interface IFacebookService
{
    Task<FacebookSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(FacebookSettings settings);

    IEnumerable<ValidationResult> ValidateSettings(FacebookSettings settings);
}
