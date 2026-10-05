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
    /// <param name="pending">
    /// A theme change to evaluate INSTEAD of the currently active theme of its bucket - how
    /// a change is checked before it is applied.
    /// </param>
    Task<IReadOnlyList<ShellIncompatibility>> GetIncompatibilitiesAsync(
        IEnumerable<string>? featureIds = null,
        PendingThemeChange? pending = null);

    /// <summary>The shells the enabled features ship pages for.</summary>
    Task<IReadOnlySet<RouteBucket>> GetRequiredBucketsAsync();
}

/// <summary>
/// A theme change not yet applied: <paramref name="ThemeId"/> becomes the active theme of
/// <paramref name="Bucket"/>, or the bucket is left with no theme when it is null (a reset).
/// </summary>
public sealed record PendingThemeChange(RouteBucket Bucket, string? ThemeId);

public sealed class ShellCompatibilityService(
    IAdminThemeService adminThemeService,
    ISiteThemeService siteThemeService,
    IMemberThemeService memberThemeService,
    IExtensionManager extensionManager,
    IShellFeaturesManager shellFeaturesManager,
    IEnumerable<IShellContractProvider> contractProviders) : IShellCompatibilityService
{
    public async Task<IReadOnlyList<ShellIncompatibility>> GetIncompatibilitiesAsync(
        IEnumerable<string>? featureIds = null,
        PendingThemeChange? pending = null)
    {
        var contracts = await ResolveContractsAsync(featureIds);
        if (contracts.Count == 0)
        {
            return [];
        }

        var activeThemes = await GetActiveThemesAsync(pending);
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

            // The chain, not the id: a fork (BaseTheme = the Crest theme) is the normal
            // way to brand a shell and must satisfy the same contract.
            if (contract.RequiresCrestBlazor && !ThemeBuckets.IsCrestBlazorTheme(activeTheme, extensionManager.GetExtension))
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

    public async Task<IReadOnlySet<RouteBucket>> GetRequiredBucketsAsync() =>
        (await ResolveContractsAsync(null)).Select(contract => contract.Contract.Bucket).ToHashSet();

    // Every contract the given features declare - explicitly, through an
    // IShellContractProvider, or implicitly, by the client libraries their module ships.
    // A null feature list means every enabled feature.
    private async Task<List<(string FeatureId, ShellContract Contract)>> ResolveContractsAsync(IEnumerable<string>? featureIds)
    {
        var wanted = featureIds is null
            ? (await shellFeaturesManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : featureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var declared = contractProviders
            .Where(provider => wanted.Contains(provider.FeatureId))
            .SelectMany(provider => provider.GetContracts().Select(contract => (provider.FeatureId, contract)));

        var shipped = extensionManager.GetFeatures()
            .Where(feature => wanted.Contains(feature.Id))
            .SelectMany(feature => ShippedContracts(feature.Extension.Id).Select(contract => (feature.Id, contract)));

        return [.. declared.Concat(shipped).Distinct()];
    }

    // A module that ships a client library for a shell needs a Crest Blazor theme for that
    // shell: its pages are Blazor components rendered by the shell's document, and under
    // any other theme they render nothing. Module client libraries are named for their
    // module ("Crest.Members.Member.BlazorWasm" belongs to "Crest.Members"), the same
    // convention the client project globs and the lazy-module generator already rely on.
    // Theme clients are excluded: a theme does not need itself.
    private static IEnumerable<ShellContract> ShippedContracts(string extensionId) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name is { } name
                && name.StartsWith(extensionId + ".", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(ModuleClientSuffix, StringComparison.Ordinal))
            .Select(ShellAssemblies.GetBucket)
            .OfType<RouteBucket>()
            .Distinct()
            .Select(bucket => new ShellContract(bucket));

    private const string ModuleClientSuffix = ".BlazorWasm";

    private async Task<Dictionary<RouteBucket, IExtensionInfo?>> GetActiveThemesAsync(PendingThemeChange? pending)
    {
        var themes = new Dictionary<RouteBucket, IExtensionInfo?>
        {
            [RouteBucket.Admin] = await adminThemeService.GetAdminThemeAsync(),
            [RouteBucket.Site] = await siteThemeService.GetSiteThemeAsync(),
            [RouteBucket.Member] = await memberThemeService.GetMemberThemeAsync(),
        };

        // A pending theme change replaces the active theme of ITS OWN bucket only: that is
        // what "would this change break anything" means.
        if (pending is not null)
        {
            themes[pending.Bucket] = pending.ThemeId is { Length: > 0 }
                && extensionManager.GetExtension(pending.ThemeId) is { Exists: true } theme
                    ? theme
                    : null;
        }

        return themes;
    }
}
