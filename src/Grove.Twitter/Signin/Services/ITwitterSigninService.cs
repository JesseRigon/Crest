using Grove.Twitter.Signin.Settings;

namespace Grove.Twitter.Signin.Services;

public interface ITwitterSigninService
{
    Task<TwitterSigninSettings> GetSettingsAsync();
    Task<TwitterSigninSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(TwitterSigninSettings settings);
}
