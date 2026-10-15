using Microsoft.Extensions.Localization;
using Grove.Display.Management.Extensions;
using Grove.Environment.Extensions;
using Grove.Environment.Shell;

namespace Grove.Themes.Services;

public class ThemeService : IThemeService
{
    private readonly IExtensionManager _extensionManager;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly ISiteThemeService _siteThemeService;
    protected readonly IStringLocalizer S;

    public ThemeService(
        IExtensionManager extensionManager,
        IShellFeaturesManager shellFeaturesManager,
        ISiteThemeService siteThemeService,
        IStringLocalizer<ThemeService> localizer)
    {
        _extensionManager = extensionManager;
        _shellFeaturesManager = shellFeaturesManager;
        _siteThemeService = siteThemeService;

        S = localizer;
    }

    public async Task DisableThemeFeaturesAsync(string themeName)
    {
        var themes = new Queue<string>();
        while (themeName != null)
        {
            if (themes.Contains(themeName))
            {
                throw new InvalidOperationException(S["The theme \"{0}\" is already in the stack of themes that need features disabled.", themeName].ToString());
            }

            var theme = _extensionManager.GetExtension(themeName);
            if (theme == null)
            {
                break;
            }

            themes.Enqueue(themeName);

            themeName = !string.IsNullOrWhiteSpace(theme.Manifest.Name)
                ? theme.Manifest.Name
                : null;
        }

        var currentTheme = await _siteThemeService.GetSiteThemeNameAsync();

        while (themes.Count > 0)
        {
            var themeId = themes.Dequeue();

            // Not disabling base theme if it's the current theme.
            if (themeId != currentTheme)
            {
                await DisableFeaturesAsync(new[] { themeId }, true);
            }
        }
    }

    public async Task EnableThemeFeaturesAsync(string themeName)
    {
        var themes = new Stack<string>();
        while (themeName != null)
        {
            if (themes.Contains(themeName))
            {
                throw new InvalidOperationException(S["The theme \"{0}\" is already in the stack of themes that need features enabled.", themeName].ToString());
            }

            themes.Push(themeName);

            // TODO: MWP: probably follow on issue: should this be recursive? maybe with a depth limit? i.e. base3->base2->base1 ...
            var extensionInfo = _extensionManager.GetExtension(themeName);
            var theme = new ThemeExtensionInfo(extensionInfo);
            // Along similar lines as with ModuleAttribute, internal safety checks handled within the attribute itself
            themeName = theme.BaseTheme;
        }

        while (themes.Count > 0)
        {
            var themeId = themes.Pop();

            await EnableFeaturesAsync(new[] { themeId }, true);
        }
    }

    /// <summary>
    /// Enables a list of features.
    /// </summary>
    /// <param name="featureIds">The IDs for the features to be enabled.</param>
    public Task EnableFeaturesAsync(IEnumerable<string> featureIds)
    {
        return EnableFeaturesAsync(featureIds, false);
    }

    /// <summary>
    /// Enables a list of features.
    /// </summary>
    /// <param name="featureIds">The IDs for the features to be enabled.</param>
    /// <param name="force">Boolean parameter indicating if the feature should enable it's dependencies if required or fail otherwise.</param>
    public async Task EnableFeaturesAsync(IEnumerable<string> featureIds, bool force)
    {
        var featuresToEnable = _extensionManager
            .GetFeatures()
            .Where(x => featureIds.Contains(x.Id));

        await _shellFeaturesManager.EnableFeaturesAsync(featuresToEnable, force);
    }

    /// <summary>
    /// Disables a list of features.
    /// </summary>
    /// <param name="featureIds">The IDs for the features to be disabled.</param>
    public Task DisableFeaturesAsync(IEnumerable<string> featureIds)
    {
        return DisableFeaturesAsync(featureIds, false);
    }

    /// <summary>
    /// Disables a list of features.
    /// </summary>
    /// <param name="featureIds">The IDs for the features to be disabled.</param>
    /// <param name="force">Boolean parameter indicating if the feature should disable the features which depend on it if required or fail otherwise.</param>
    public async Task DisableFeaturesAsync(IEnumerable<string> featureIds, bool force)
    {
        var featuresToDisable = _extensionManager
            .GetFeatures()
            .Where(x => featureIds.Contains(x.Id));

        await _shellFeaturesManager.DisableFeaturesAsync(featuresToDisable, force);
    }
}
