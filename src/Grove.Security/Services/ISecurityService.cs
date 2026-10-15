using Grove.Security.Settings;

namespace Grove.Security.Services;

public interface ISecurityService
{
    Task<SecuritySettings> GetSettingsAsync();
}
