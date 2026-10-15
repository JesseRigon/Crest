using Grove.Https.Settings;

namespace Grove.Https.Services;

public interface IHttpsService
{
    Task<HttpsSettings> GetSettingsAsync();
}
