using Grove.Integrations.Twitter.Signin.Settings;

namespace Grove.Integrations.Twitter.Signin.Services;

public interface ITwitterSigninService
{
    Task<TwitterSigninSettings> GetSettingsAsync();
    Task<TwitterSigninSettings> LoadSettingsAsync();
    Task UpdateSettingsAsync(TwitterSigninSettings settings);
}
