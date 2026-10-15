#nullable enable

using Grove.Entities;
using Grove.Environment.Extensions;
using Grove.Infrastructure.Settings;

namespace Grove.Themes;

/// <summary>The tenant's selected member theme, stored with the site settings.</summary>
public sealed class GroveMemberThemeSettings
{
    public string? ThemeId { get; set; }
}

/// <summary>
/// The member-shell counterpart of Grove's <c>ISiteThemeService</c> and
/// <c>IAdminThemeService</c>.
/// </summary>
/// <remarks>
/// Grove has a site theme and an admin theme and no member theme, so Grove keeps this
/// one setting itself, in the site settings where Grove keeps the other two. Without it a
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
    /// <summary>Grove's own member theme, used until a tenant selects another.</summary>
    public const string DefaultThemeId = "Grove.Themes.Members";

    public async Task<string> GetMemberThemeIdAsync()
    {
        var settings = await siteService.GetSettingsAsync<GroveMemberThemeSettings>();
        return string.IsNullOrWhiteSpace(settings.ThemeId) ? DefaultThemeId : settings.ThemeId;
    }

    public async Task<IExtensionInfo?> GetMemberThemeAsync() =>
        extensionManager.GetExtension(await GetMemberThemeIdAsync()) is { Exists: true } theme ? theme : null;

    public async Task SetMemberThemeAsync(string themeId)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<GroveMemberThemeSettings>(settings => settings.ThemeId = themeId);
        await siteService.UpdateSiteSettingsAsync(site);
    }
}
