using Crest.Environment.Extensions;

namespace Crest.Admin;

public interface IAdminThemeService
{
    Task<IExtensionInfo> GetAdminThemeAsync();
    Task SetAdminThemeAsync(string themeName);
    Task<string> GetAdminThemeNameAsync();
}
