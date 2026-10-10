using Crest.Twitter.Signin.Settings;

namespace Crest.Twitter.Signin.Services;

public interface ITwitterSigninService
{
    Task<TwitterSigninSettings> GetSettingsAsync();
    Task<TwitterSigninSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(TwitterSigninSettings settings);
}
