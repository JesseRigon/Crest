using System.ComponentModel.DataAnnotations;
using Grove.Integrations.Facebook.Settings;

namespace Grove.Integrations.Facebook.Services;

public interface IFacebookService
{
    Task<FacebookSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(FacebookSettings settings);

    IEnumerable<ValidationResult> ValidateSettings(FacebookSettings settings);
}
