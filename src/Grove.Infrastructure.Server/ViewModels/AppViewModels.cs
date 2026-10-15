using Grove.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Infrastructure.Shells.Admin;
using Grove.Infrastructure.Shells.Admin.Models;
using Grove.Environment.Extensions;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Descriptor;
using Grove.Entities;
using Grove.Environment.Shell.Scope;
using Grove.Display.Navigation;
using Grove.Infrastructure.Settings;
using Grove.Access.Users;
using Grove.Access.Users.Localization.Models;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Grove.Data.Localization;
using Grove.Data.Localization.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Grove.Infrastructure.Shells.Admin.Api;
using Grove.Infrastructure.Features.Api;

namespace Grove.ViewModels;

public sealed record AppManifest(
    Tenant Tenant,
    Tenant[] Tenants,
    Grove.Infrastructure.Settings.Api.SiteSettingsModel Site,
    AdminSettingsDto AdminSettings,
    TitleBarSettingsDto TitleBarSettings,
    AdminDescriptor Admin,
    int FeatureSerialNumber,
    string FeatureHash,
    Feature[] Features,
    NavigationMenu AdminMenu,
    Grove.Access.RouteAccess[] AuthorizedRoutes,
    CultureSelector CultureSelector,
    NavigationMenu ProfileMenu,
    // The signed-in user's organizations on this tenant (a dual user's member bindings), for
    // the shell switcher; server data, never a list of other tenants.
    Grove.Routing.ShellOrganization[] Organizations);

public sealed record CultureSelector(
    string? UserDefaultCulture,
    string TenantDefaultCulture,
    // Rung 3 - see GroveLocalizationSettings.AdminDefaultCulture (Grove.Data.Localization).
    // Null means no admin-specific override is configured for this tenant.
    string? AdminDefaultCulture,
    CultureOption[] Cultures,
    string CookieName,
    string CookiePath)
{
    // The server does NOT resolve culture - it has no way to. The session override (rung 1
    // of the priority chain) lives only in the browser's sessionStorage, which the server
    // can never see; only the client can weigh it against everything else. This type is
    // deliberately just raw inputs - the tenant's supported cultures + default, and the
    // signed-in user's stored default (if any) - for DisplayManager.ResolveCultureAsync to
    // resolve from. See docs/localization.md's "Resolution architecture" section.
    // (Earlier revisions of this type also carried a server-computed CurrentCulture field -
    // removed, since its mere presence invited reading it as an authoritative answer even
    // though nothing ever consumed it that way.)
    public static async Task<CultureSelector> FromAsync(
        HttpContext httpContext,
        ShellSettings shellSettings,
        ILocalizationService? localizationService,
        string? userDefaultCulture,
        string? adminDefaultCulture)
    {
        var supportedCultures = localizationService is null
            ? []
            : await localizationService.GetSupportedCulturesAsync();
        var tenantDefaultCulture = localizationService is null
            ? CultureInfo.CurrentUICulture.Name
            : await localizationService.GetDefaultCultureAsync();

        return new CultureSelector(
            userDefaultCulture,
            tenantDefaultCulture,
            adminDefaultCulture,
            supportedCultures
                .Select(CultureInfo.GetCultureInfo)
                .Select(culture => new CultureOption(culture.Name, culture.NativeName, GetIcon(culture)))
                .ToArray(),
            CultureCookie.MakeCookieName(shellSettings),
            CultureCookie.MakeCookiePath(httpContext));
    }

    private static string GetIcon(CultureInfo culture)
    {
        var region = culture.Name.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(part => part.Length == 2);
        region ??= culture.TwoLetterISOLanguageName switch
        {
            "en" => "us", "pt" => "pt", "zh" => "cn", "ar" => "sa", _ => null,
        };

        // Country flags are normal Iconify references, so they go through the
        // same server-resolved pack as every other Grove icon.
        return region is null
            ? "iconify.mdi/current/default/translate"
            : $"iconify.circle-flags/current/default/{region.ToLowerInvariant()}";
    }
}

public sealed record CultureOption(string Value, string Label, string Icon);

public sealed record Tenant(
    string Name,
    string TenantId,
    string State,
    string? RequestUrlHost,
    string[] RequestUrlHosts,
    string? RequestUrlPrefix)
{
    public static Tenant From(ShellSettings settings) => new(
        settings.Name,
        settings.TenantId,
        settings.State.ToString(),
        settings.RequestUrlHost,
        settings.RequestUrlHosts ?? [],
        settings.RequestUrlPrefix);
}

public sealed record AdminDescriptor(string BasePath);

