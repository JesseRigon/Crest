using System.ComponentModel.DataAnnotations;
using Grove.Integrations.Twitter.Settings;

namespace Grove.Integrations.Twitter.Services;

public interface ITwitterSettingsService
{
    Task<TwitterSettings> GetSettingsAsync();
    Task<TwitterSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(TwitterSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(TwitterSettings settings);
}
