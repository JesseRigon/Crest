using Grove.Data.Protocols.Https.Settings;

namespace Grove.Data.Protocols.Https.Services;

public interface IHttpsService
{
    Task<HttpsSettings> GetSettingsAsync();
}
