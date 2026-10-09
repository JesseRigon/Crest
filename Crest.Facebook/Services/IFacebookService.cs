using System.ComponentModel.DataAnnotations;
using Crest.Facebook.Settings;

namespace Crest.Facebook.Services;

public interface IFacebookService
{
    Task<FacebookSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(FacebookSettings settings);

    IEnumerable<ValidationResult> ValidateSettings(FacebookSettings settings);
}
