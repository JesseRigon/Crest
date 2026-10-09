using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.Localization;

/// <summary>
/// Provides extension methods for <see cref="IPlatformHelper"/> related to JavaScript localization.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public static class LocalizationPlatformHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Returns a merged dictionary of JavaScript localizations for the specified groups by aggregating all
    /// registered <see cref="IJSLocalizer"/> implementations.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="groups">
    /// One or more group identifiers used to scope the set of translation keys to return.
    /// Each <see cref="IJSLocalizer"/> implementation decides which groups it handles.
    /// </param>
    /// <returns>
    /// A merged dictionary of translation keys and their localized values.
    /// If multiple <see cref="IJSLocalizer"/> implementations provide a value for the same key, the last
    /// registered implementation wins.
    /// </returns>
    /// <example>
    /// In a Razor view or layout:
    /// <code>
    /// var localizations = Platform.GetJSLocalizations("media-gallery");
    /// </code>
    /// </example>
    public static IDictionary<string, string> GetJSLocalizations(this IPlatformHelper platformHelper, params string[] groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return platformHelper.HttpContext.RequestServices
            .GetServices<IJSLocalizer>()
            .GetMergedLocalizations(groups);
    }
}
