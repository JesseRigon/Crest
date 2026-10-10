using Crest.Https.Settings;

namespace Crest.Https.Services;

public interface IHttpsService
{
    Task<HttpsSettings> GetSettingsAsync();
}
