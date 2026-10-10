using Crest.Environment.Extensions;
using Crest.Settings;

namespace Crest.Admin;

public class AdminThemeService : IAdminThemeService
{
    private readonly ISiteService _siteService;
    private readonly IExtensionManager _extensionManager;

    public AdminThemeService(
        ISiteService siteService,
        IExtensionManager extensionManager)
    {
        _siteService = siteService;
        _extensionManager = extensionManager;
    }

    public async Task<IExtensionInfo> GetAdminThemeAsync()
    {
        var currentThemeName = await GetAdminThemeNameAsync();
        if (string.IsNullOrEmpty(currentThemeName))
        {
            return null;
        }

        return _extensionManager.GetExtension(currentThemeName);
    }

    public async Task SetAdminThemeAsync(string themeName)
    {
        var site = await _siteService.LoadSiteSettingsAsync();
        site.Properties["CurrentAdminThemeName"] = themeName;
        await _siteService.UpdateSiteSettingsAsync(site);
    }

    public async Task<string> GetAdminThemeNameAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        return (string)site.Properties["CurrentAdminThemeName"];
    }
}
