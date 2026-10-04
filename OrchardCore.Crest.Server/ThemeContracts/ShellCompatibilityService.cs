using OrchardCore.Admin;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Themes.Services;
using Crest.Routing;

namespace Crest.Themes;

/// <summary>
/// Answers whether the tenant's active themes satisfy the shell contracts its features
/// declare.
/// </summary>
/// <remarks>
/// Themes are not interchangeable: a Crest shell is a Blazor Web App document plus a route
/// bucket plus a design system, and a module's pages are written against that. A host's
/// admin pages do not work under an arbitrary Orchard theme, so the system says so rather
/// than rendering a broken shell.
///
/// Both directions are guarded, with deliberately different severity - see
/// <c>plans/shells-and-themes.md</c>:
/// <list type="bullet">
/// <item>enabling a feature whose contract is unsatisfiable is REFUSED: it would render
/// nothing usable, and the admin has not asked for that outcome;</item>
/// <item>changing a theme WARNS and proceeds on explicit acknowledgement: the active theme
/// is the admin's own decision about their site, and refusing it would strand a tenant
/// whose theme was removed or whose fork is unrecognized.</item>
/// </list>
/// </remarks>
public interface IShellCompatibilityService
{
    /// <summary>
    /// The contracts these features declare that the given active themes do not satisfy.
    /// </summary>
    /// <param name="featureIds">Features to check; null checks every enabled feature.</param>
    /// <param name="overrideThemeId">
    /// A theme to evaluate INSTEAD of the currently active one for its bucket - how a
    /// pending theme change is checked before it is applied.
    /// </param>
    Task<IReadOnlyList<ShellIncompatibility>> GetIncompatibilitiesAsync(
        IEnumerable<string>? featureIds = null,
        string? overrideThemeId = null);
}

public sealed class ShellCompatibilityService(
    IAdminThemeService adminThemeService,
    ISiteThemeService siteThemeService,
    IExtensionManager extensionManager,
    IShellFeaturesManager shellFeaturesManager,
    IEnumerable<IShellContractProvider> contractProviders,
    Microsoft.Extensions.Options.IOptions<MemberOptions> memberOptions) : IShellCompatibilityService
{
    public async Task<IReadOnlyList<ShellIncompatibility>> GetIncompatibilitiesAsync(
        IEnumerable<string>? featureIds = null,
        string? overrideThemeId = null)
    {
        var contracts = await ResolveContractsAsync(featureIds);
        if (contracts.Count == 0)
        {
            return [];
        }

        var activeThemes = await GetActiveThemesAsync(overrideThemeId);
        var incompatibilities = new List<ShellIncompatibility>();

        foreach (var (featureId, contract) in contracts)
        {
            activeThemes.TryGetValue(contract.Bucket, out var activeTheme);

            if (activeTheme is null)
            {
                incompatibilities.Add(new ShellIncompatibility(
                    featureId,
                    contract.Bucket,
                    null,
                    $"No {contract.Bucket} theme is active."));
                continue;
            }

            if (!contract.RequiresCrestBlazor)
            {
                continue;
            }

            // The chain, not the id: a fork (BaseTheme = the Crest theme) is the normal
            // way to brand a shell and must satisfy the same contract.
            var satisfied = ThemeBuckets
                .WalkBaseThemeChain(activeTheme, id => extensionManager.GetExtension(id))
                .Any(theme => ThemeBuckets.IsCrestBlazor(theme.Manifest));

            if (!satisfied)
            {
                incompatibilities.Add(new ShellIncompatibility(
                    featureId,
                    contract.Bucket,
                    activeTheme.Id,
                    $"'{activeTheme.Id}' is not a Crest Blazor {contract.Bucket} theme, and none of its base themes is."));
            }
        }

        return incompatibilities;
    }

    private async Task<List<(string FeatureId, ShellContract Contract)>> ResolveContractsAsync(IEnumerable<string>? featureIds)
    {
        var wanted = featureIds is null
            ? (await shellFeaturesManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : featureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. contractProviders
                .Where(provider => wanted.Contains(provider.FeatureId))
                .SelectMany(provider => provider.GetContracts().Select(contract => (provider.FeatureId, contract)))
        ];
    }

    private async Task<Dictionary<RouteBucket, IExtensionInfo?>> GetActiveThemesAsync(string? overrideThemeId)
    {
        var themes = new Dictionary<RouteBucket, IExtensionInfo?>
        {
            [RouteBucket.Admin] = await adminThemeService.GetAdminThemeAsync(),
            [RouteBucket.Site] = await siteThemeService.GetSiteThemeAsync(),
            // The member theme is resolved by id from options rather than from an Orchard
            // theme service, because Orchard has site and admin theme settings and no
            // member one. A tenant selecting a member theme is Crest's own setting.
            [RouteBucket.Member] = extensionManager.GetExtension(memberOptions.Value.MemberThemeId),
        };

        // A pending theme change replaces the active theme of ITS OWN bucket only: that is
        // what "would this change break anything" means.
        if (overrideThemeId is { Length: > 0 }
            && extensionManager.GetExtension(overrideThemeId) is { } pending)
        {
            themes[ThemeBuckets.GetBucket(pending.Manifest)] = pending;
        }

        return themes;
    }
}
