using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.DisplayManagement.Extensions;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Themes.Services;
using Crest.Components.Modules;
using Crest.Routing;
using Crest.Themes;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/themes")]
public sealed class ThemesController(
    ISiteThemeService siteThemeService,
    IAdminThemeService adminThemeService,
    IMemberThemeService memberThemeService,
    IShellFeaturesManager shellFeaturesManager,
    IExtensionManager extensionManager,
    IShellCompatibilityService shellCompatibility,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ThemesState>> List()
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Themes.Permissions.ApplyTheme))
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
                ThemeBuckets.IsCrestBlazorTheme(feature.Extension, extensionManager.GetExtension),
                [.. ThemeBuckets.WalkBaseThemeChain(feature.Extension, extensionManager.GetExtension).Skip(1).Select(theme => theme.Id)],
                isCurrent,
                enabledIds.Contains(feature.Id),
                $"/{feature.Extension.Id}/Theme.png",
                [.. incompatibilities.Where(incompatibility => incompatibility.Bucket == bucket)]));
        }

        ThemeShell[] shells =
        [
            new(CrestShells.Site, current[RouteBucket.Site], CanReset: true),
            new(CrestShells.Admin, current[RouteBucket.Admin], CanReset: true),
            .. requiredBuckets.Contains(RouteBucket.Member)
                ? [new ThemeShell(CrestShells.Member, current[RouteBucket.Member], CanReset: false)]
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
    [HttpPost("{id}/current")]
    public async Task<IActionResult> SetCurrent(string id, [FromQuery] bool acknowledgeIncompatibilities = false)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Themes.Permissions.ApplyTheme))
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

        switch (bucket)
        {
            case RouteBucket.Admin:
                await adminThemeService.SetAdminThemeAsync(feature.Id);
                break;
            case RouteBucket.Member:
                await memberThemeService.SetMemberThemeAsync(feature.Id);
                break;
            default:
                await siteThemeService.SetSiteThemeAsync(feature.Id);
                break;
        }

        var enabledFeatures = await shellFeaturesManager.GetEnabledFeaturesAsync();
        if (!enabledFeatures.Any(enabled => string.Equals(enabled.Id, feature.Id, StringComparison.OrdinalIgnoreCase)))
        {
            await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        }

        return NoContent();
    }

    [HttpPost("reset-site")]
    public async Task<IActionResult> ResetSiteTheme([FromQuery] bool acknowledgeIncompatibilities = false)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        if (await RefuseUnacknowledgedAsync(new PendingThemeChange(RouteBucket.Site, null), acknowledgeIncompatibilities) is { } refusal)
        {
            return refusal;
        }

        await siteThemeService.SetSiteThemeAsync(string.Empty);
        return NoContent();
    }

    [HttpPost("reset-admin")]
    public async Task<IActionResult> ResetAdminTheme([FromQuery] bool acknowledgeIncompatibilities = false)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        if (await RefuseUnacknowledgedAsync(new PendingThemeChange(RouteBucket.Admin, null), acknowledgeIncompatibilities) is { } refusal)
        {
            return refusal;
        }

        await adminThemeService.SetAdminThemeAsync(string.Empty);
        return NoContent();
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Themes.Permissions.ApplyTheme))
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

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Themes.Permissions.ApplyTheme))
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
    private async Task<IActionResult?> RefuseUnacknowledgedAsync(PendingThemeChange change, bool acknowledged)
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
                $"This is a breaking change: it breaks {broken.Length} enabled feature(s) that need a Crest Blazor {ShellOf(change.Bucket)} theme.",
                broken));
    }

    private async Task<Dictionary<RouteBucket, string?>> GetCurrentThemeIdsAsync() => new()
    {
        [RouteBucket.Site] = (await siteThemeService.GetSiteThemeAsync())?.Id,
        [RouteBucket.Admin] = (await adminThemeService.GetAdminThemeAsync())?.Id,
        [RouteBucket.Member] = await memberThemeService.GetMemberThemeIdAsync(),
    };

    private static string ShellOf(RouteBucket bucket) => bucket switch
    {
        RouteBucket.Admin => CrestShells.Admin,
        RouteBucket.Member => CrestShells.Member,
        _ => CrestShells.Site,
    };

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
