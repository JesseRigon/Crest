using Grove.Environment.Extensions;

namespace Grove.Themes.Services;

public interface ISiteThemeService
{
    Task<IExtensionInfo> GetSiteThemeAsync();

    Task SetSiteThemeAsync(string themeName);

    Task<string> GetSiteThemeNameAsync();
}
