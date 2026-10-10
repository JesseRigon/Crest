using Crest.Security.Settings;

namespace Crest.Security.Services;

public interface ISecurityService
{
    Task<SecuritySettings> GetSettingsAsync();
}
