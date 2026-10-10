using System.ComponentModel.DataAnnotations;
using Crest.Twitter.Settings;

namespace Crest.Twitter.Services;

public interface ITwitterSettingsService
{
    Task<TwitterSettings> GetSettingsAsync();
    Task<TwitterSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(TwitterSettings settings);
    IEnumerable<ValidationResult> ValidateSettings(TwitterSettings settings);
}
