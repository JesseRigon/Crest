using Crest.Environment.Extensions;

namespace Crest.Themes.Services;

public interface ISiteThemeService
{
    Task<IExtensionInfo> GetSiteThemeAsync();

    Task SetSiteThemeAsync(string themeName);

    Task<string> GetSiteThemeNameAsync();
}
