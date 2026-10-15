using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Infrastructure.Shells.Admin;
using Grove.Infrastructure.Shells.Admin.Models;
using Grove.Infrastructure.Shells.Admin.Api;
using Grove.Infrastructure.Features.Api;
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
using Grove.ViewModels;
using SiteSettings = Grove.Infrastructure.Settings.Api.SiteSettingsModel;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/app</c> endpoints.</summary>
public sealed class AppApi : ApiContributor
{
    public override string Name => "App";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("app");
        routes.MapGet("manifest", ApiHandlers.Handle<AppHandlers>(h => h.GetManifest()));
    }
}

public sealed class AppHandlers(
    ShellSettings shellSettings,
    IShellDescriptorManager shellDescriptorManager,
    IExtensionManager extensionManager,
    ISiteService siteService,
    IOptions<AdminOptions> adminOptions,
    IAuthorizationService authorization,
    AdminMenuBuilder adminMenuBuilder,
    PrimaryNavMenuSettingsStore primaryNavMenuSettingsStore,
    AdminSettingsNormalizer adminSettingsNormalizer,
    TitleBarSettingsStore titleBarSettingsStore,
    IconController iconController,
    Grove.Access.IRouteAuthorization routeAuthorization,
    Grove.Access.ICallerContextAccessor callers,
    ProfileMenuService profileMenuService,
    UserManager<IUser> userManager,
    IServiceProvider serviceProvider) : ApiHandlers
{
    public async Task<IResult> GetManifest()
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel)) return Forbid();
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var site = await siteService.GetSiteSettingsAsync();
        var adminSettings = await adminSettingsNormalizer.EnsureNewMenuEnabledAsync();
        var titleBarSettings = await titleBarSettingsStore.GetAsync(HttpContext.RequestAborted);
        var featureIds = descriptor.Features.Select(feature => feature.Id).Order(StringComparer.Ordinal).ToArray();
        var featureInfos = extensionManager.GetFeatures(featureIds.AsEnumerable()).ToDictionary(feature => feature.Id);
        // Only this tenant: the tenants in the switcher are the accounts added on this device,
        // kept in the browser (docs/shells-and-themes.md › Member sessions and sides). No
        // server data links accounts across tenants, and no other tenant's shell is started
        // to ask whether a user of the same name exists there.
        Tenant[] tenants = [Tenant.From(shellSettings)];
        var organizations = serviceProvider.GetService<Grove.Routing.IShellOrganizationResolver>() is { } organizationResolver
            ? [.. await organizationResolver.ListAsync(HttpContext)]
            : Array.Empty<Grove.Routing.ShellOrganization>();
        var userDefaultCulture = await GetUserDefaultCultureAsync();
        var adminDefaultCulture = site.GetOrCreate<GroveLocalizationSettings>().AdminDefaultCulture;
        var cultureSelector = await CultureSelector.FromAsync(HttpContext, shellSettings, serviceProvider.GetService<ILocalizationService>(), userDefaultCulture, adminDefaultCulture);
        var profileMenu = await profileMenuService.BuildAsync(User, HttpContext.RequestAborted);
        var adminMenu = await adminMenuBuilder.BuildAsync(HttpContext, User);
        adminMenu = adminMenu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        adminMenu = await iconController.ResolveMenuIconsAsync(
            adminMenu,
            IconController.AdminMenuChromeIconKeys
                .Concat(cultureSelector.Cultures.Select(culture => culture.Icon))
                .Concat([
                    "iconify.mdi/current/default/check",
                    "iconify.mdi/current/default/weather-night",
                    "iconify.mdi/current/default/weather-sunny",
                ]),
            HttpContext.RequestAborted);

        return Ok(new AppManifest(
            Tenant.From(shellSettings),
            tenants,
            SiteSettings.From(site),
            AdminSettingsDto.From(adminSettings),
            TitleBarSettingsDto.From(titleBarSettings),
            new AdminDescriptor(NormalizeAdminPath(adminOptions.Value.AdminUrlPrefix)),
            descriptor.SerialNumber,
            ComputeFeatureHash(descriptor.SerialNumber, featureIds),
            featureIds.Select(id => Feature.From(id, featureInfos.GetValueOrDefault(id))).ToArray(),
            adminMenu,
            await routeAuthorization.GetAuthorizedRoutesAsync(callers.Current, HttpContext.RequestAborted),
            cultureSelector,
            profileMenu,
            organizations));
    }

    private async Task<string?> GetUserDefaultCultureAsync()
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return null;
        }

        user.TryGet<UserLocalizationSettings>(out var settings);
        return settings?.Culture;
    }

    private static string NormalizeAdminPath(string? adminUrlPrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(adminUrlPrefix) ? "admin" : adminUrlPrefix.Trim('/');
        return '/' + prefix;
    }

    private static string ComputeFeatureHash(int serialNumber, IEnumerable<string> featureIds)
    {
        var input = $"{serialNumber}:{string.Join('|', featureIds)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
