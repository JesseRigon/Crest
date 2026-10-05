using OrchardCore.Entities;
using OrchardCore.Environment.Extensions;
using OrchardCore.Settings;

namespace Crest.Themes;

/// <summary>The tenant's selected member theme, stored with the site settings.</summary>
public sealed class CrestMemberThemeSettings
{
    public string? ThemeId { get; set; }
}

/// <summary>
/// The member-shell counterpart of Orchard's <c>ISiteThemeService</c> and
/// <c>IAdminThemeService</c>.
/// </summary>
/// <remarks>
/// Orchard has a site theme and an admin theme and no member theme, so Crest keeps this
/// one setting itself, in the site settings where Orchard keeps the other two. Without it a
/// member theme could only be "selected" by making it the site theme - which would put the
/// member theme on the public site.
/// </remarks>
public interface IMemberThemeService
{
    Task<string> GetMemberThemeIdAsync();

    Task<IExtensionInfo?> GetMemberThemeAsync();

    Task SetMemberThemeAsync(string themeId);
}

public sealed class MemberThemeService(ISiteService siteService, IExtensionManager extensionManager) : IMemberThemeService
{
    /// <summary>Crest's own member theme, used until a tenant selects another.</summary>
    public const string DefaultThemeId = "Crest.Member";

    public async Task<string> GetMemberThemeIdAsync()
    {
        var settings = await siteService.GetSettingsAsync<CrestMemberThemeSettings>();
        return string.IsNullOrWhiteSpace(settings.ThemeId) ? DefaultThemeId : settings.ThemeId;
    }

    public async Task<IExtensionInfo?> GetMemberThemeAsync() =>
        extensionManager.GetExtension(await GetMemberThemeIdAsync()) is { Exists: true } theme ? theme : null;

    public async Task SetMemberThemeAsync(string themeId)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<CrestMemberThemeSettings>(settings => settings.ThemeId = themeId);
        await siteService.UpdateSiteSettingsAsync(site);
    }
}
