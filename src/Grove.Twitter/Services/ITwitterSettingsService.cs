using System.ComponentModel.DataAnnotations;
using Grove.Twitter.Settings;

namespace Grove.Twitter.Services;

public interface ITwitterSettingsService
{
    Task<TwitterSettings> GetSettingsAsync();
    Task<TwitterSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(TwitterSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(TwitterSettings settings);
}
