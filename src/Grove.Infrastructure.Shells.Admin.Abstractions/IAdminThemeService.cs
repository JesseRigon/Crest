using Grove.Environment.Extensions;

namespace Grove.Infrastructure.Shells.Admin;

public interface IAdminThemeService
{
    Task<IExtensionInfo> GetAdminThemeAsync();
    Task SetAdminThemeAsync(string themeName);
    Task<string> GetAdminThemeNameAsync();
}
