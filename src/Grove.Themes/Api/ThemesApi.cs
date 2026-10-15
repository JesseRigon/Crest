#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Infrastructure.Shells.Admin;
using Grove.Display.Management.Extensions;
using Grove.Environment.Extensions;
using Grove.Environment.Extensions.Features;
using Grove.Environment.Shell;
using Grove.Themes.Services;
using Grove.Display.Components.Modules;
using Grove.Routing;
using Grove.Themes;

namespace Grove.Themes.Api;

public sealed class ThemesApi : ApiContributor
{
    public override string Name => "Themes";

    public override string FeatureId => "Grove.Themes";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("themes");
        routes.MapGet("", ApiHandlers.Handle<ThemesHandlers>(h => h.List()));
        routes.MapPost("{id}/current", (HttpContext __http, [FromRoute] string id, [FromQuery] bool acknowledgeIncompatibilities = false) => ApiHandlers.For<ThemesHandlers>(__http).SetCurrent(id, acknowledgeIncompatibilities));
        routes.MapPost("reset-site", (HttpContext __http, [FromQuery] bool acknowledgeIncompatibilities = false) => ApiHandlers.For<ThemesHandlers>(__http).ResetSiteTheme(acknowledgeIncompatibilities));
        routes.MapPost("reset-admin", (HttpContext __http, [FromQuery] bool acknowledgeIncompatibilities = false) => ApiHandlers.For<ThemesHandlers>(__http).ResetAdminTheme(acknowledgeIncompatibilities));
        routes.MapPost("{id}/enable", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<ThemesHandlers>(__http).Enable(id));
        routes.MapPost("{id}/disable", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<ThemesHandlers>(__http).Disable(id));
    }
}

public sealed class ThemesHandlers(
    ISiteThemeService siteThemeService,
    IAdminThemeService adminThemeService,
    IMemberThemeService memberThemeService,
    IShellFeaturesManager shellFeaturesManager,
    IExtensionManager extensionManager,
    IShellCompatibilityService shellCompatibility,
    IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> List()
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var current = await GetCurrentThemeIdsAsync();
        var enabledIds = (await shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredBuckets = await shellCompatibility.GetRequiredBucketsAsync();
        var failingNow = await shellCompatibility.GetIncompatibilitiesAsync();

        var themes = new List<ThemeSummary>();
        foreach (var feature in (await shellFeaturesManager.GetAvailableFeaturesAsync()).Where(IsSelectableTheme))
        {
            var bucket = ThemeBuckets.GetBucket(feature.Extension.Manifest);
            var isCurrent = string.Equals(feature.Id, current[bucket], StringComparison.OrdinalIgnoreCase);
            var incompatibilities = isCurrent
                ? failingNow
                : await shellCompatibility.GetIncompatibilitiesAsync(pending: new PendingThemeChange(bucket, feature.Id));

            themes.Add(new ThemeSummary(
                feature.Id,
                feature.Name ?? feature.Id,
                feature.Description ?? string.Empty,
                feature.Extension.Manifest.Author ?? string.Empty,
                feature.Extension.Manifest.Website ?? string.Empty,
                feature.Extension.Manifest.Version ?? string.Empty,
                feature.Extension.Id,
                ShellOf(bucket),
                ThemeBuckets.IsGroveBlazorTheme(feature.Extension, extensionManager.GetExtension),
                [.. ThemeBuckets.WalkBaseThemeChain(feature.Extension, extensionManager.GetExtension).Skip(1).Select(theme => theme.Id)],
                isCurrent,
                enabledIds.Contains(feature.Id),
                $"/{feature.Extension.Id}/Theme.png",
                [.. incompatibilities.Where(incompatibility => incompatibility.Bucket == bucket)]));
        }

        ThemeShell[] shells =
        [
            new(GroveShells.Site, current[ShellId.Site], CanReset: true),
            new(GroveShells.Admin, current[ShellId.Admin], CanReset: true),
            .. requiredBuckets.Contains(ShellId.Member)
                ? [new ThemeShell(GroveShells.Member, current[ShellId.Member], CanReset: false)]
                : Array.Empty<ThemeShell>(),
        ];

        return Ok(new ThemesState(
            shells,
            [.. themes.OrderByDescending(theme => theme.IsCurrent).ThenBy(theme => theme.Name)]));
    }

    // A theme change warns and proceeds rather than being refused: the active theme is the
    // admin's own decision about their site. But the API carries the warning itself, not
    // just the UI - a change that would break enabled features is rejected with the report
    // unless the caller acknowledges it, so a script or recipe gets the same gate a person
    // does. See docs/shells-and-themes.md, "The two guards".
    public async Task<IResult> SetCurrent(string id, [FromQuery] bool acknowledgeIncompatibilities = false)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var feature = await FindThemeAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        var bucket = ThemeBuckets.GetBucket(feature.Extension.Manifest);
        if (await RefuseUnacknowledgedAsync(new PendingThemeChange(bucket, feature.Id), acknowledgeIncompatibilities) is { } refusal)
        {
            return refusal;
        }

        if (bucket == ShellId.Admin)
        {
            await adminThemeService.SetAdminThemeAsync(feature.Id);
        }
        else if (bucket == ShellId.Member)
        {
            await memberThemeService.SetMemberThemeAsync(feature.Id);
        }
        else
        {
            await siteThemeService.SetSiteThemeAsync(feature.Id);
        }

        var enabledFeatures = await shellFeaturesManager.GetEnabledFeaturesAsync();
        if (!enabledFeatures.Any(enabled => string.Equals(enabled.Id, feature.Id, StringComparison.OrdinalIgnoreCase)))
        {
            await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        }

        return NoContent();
    }

    public async Task<IResult> ResetSiteTheme([FromQuery] bool acknowledgeIncompatibilities = false)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        if (await RefuseUnacknowledgedAsync(new PendingThemeChange(ShellId.Site, null), acknowledgeIncompatibilities) is { } refusal)
        {
            return refusal;
        }

        await siteThemeService.SetSiteThemeAsync(string.Empty);
        return NoContent();
    }

    public async Task<IResult> ResetAdminTheme([FromQuery] bool acknowledgeIncompatibilities = false)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        if (await RefuseUnacknowledgedAsync(new PendingThemeChange(ShellId.Admin, null), acknowledgeIncompatibilities) is { } refusal)
        {
            return refusal;
        }

        await adminThemeService.SetAdminThemeAsync(string.Empty);
        return NoContent();
    }

    public async Task<IResult> Enable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var feature = await FindThemeAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    public async Task<IResult> Disable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var feature = await FindThemeAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        await shellFeaturesManager.DisableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    // Only the features the change itself breaks are reported: a feature already failing
    // under the current theme is not news, and is already badged in the features list.
    private async Task<IResult?> RefuseUnacknowledgedAsync(PendingThemeChange change, bool acknowledged)
    {
        if (acknowledged)
        {
            return null;
        }

        var failingNow = (await shellCompatibility.GetIncompatibilitiesAsync())
            .Select(incompatibility => (incompatibility.FeatureId, incompatibility.Bucket))
            .ToHashSet();
        var broken = (await shellCompatibility.GetIncompatibilitiesAsync(pending: change))
            .Where(incompatibility => !failingNow.Contains((incompatibility.FeatureId, incompatibility.Bucket)))
            .ToArray();

        return broken.Length == 0
            ? null
            : Conflict(new ShellIncompatibilityReport(
                $"This is a breaking change: it breaks {broken.Length} enabled feature(s) that need a Grove Blazor {ShellOf(change.Bucket)} theme.",
                broken));
    }

    private async Task<Dictionary<ShellId, string?>> GetCurrentThemeIdsAsync() => new()
    {
        [ShellId.Site] = (await siteThemeService.GetSiteThemeAsync())?.Id,
        [ShellId.Admin] = (await adminThemeService.GetAdminThemeAsync())?.Id,
        [ShellId.Member] = await memberThemeService.GetMemberThemeIdAsync(),
    };

    private static string ShellOf(ShellId bucket) => bucket.Value;

    private async Task<IFeatureInfo?> FindThemeAsync(string id) => (await shellFeaturesManager.GetAvailableFeaturesAsync())
        .FirstOrDefault(feature => string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase) && IsSelectableTheme(feature));

    private static bool IsSelectableTheme(IFeatureInfo feature)
    {
        if (feature.IsAlwaysEnabled || feature.EnabledByDependencyOnly || !feature.IsTheme())
        {
            return false;
        }

        return !feature.Extension.Manifest.Tags.Any(tag => string.Equals(tag, "hidden", StringComparison.OrdinalIgnoreCase));
    }
}
