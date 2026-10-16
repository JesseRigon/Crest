using Grove.Integrations.Facebook.Settings;

namespace Grove.Integrations.Facebook.Services;

public interface IFacebookService
{
    Task<FacebookSettings> GetSettingsAsync();

    Task UpdateSettingsAsync(FacebookSettings settings);

    /// <summary>The Meta app id (the app credentials' client id), for the JavaScript SDK; null when not set.</summary>
    Task<string> GetAppIdAsync();
}
