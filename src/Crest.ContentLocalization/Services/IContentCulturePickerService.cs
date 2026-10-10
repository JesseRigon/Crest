using Microsoft.AspNetCore.Http;
using Crest.ContentLocalization.Models;

namespace Crest.ContentLocalization.Services;

public interface IContentCulturePickerService
{
    /// <summary>
    /// Get the ContentItemId that matches the url.
    /// </summary>
    /// <returns>ContentItemId or null if not found.</returns>
    Task<string> GetContentItemIdFromRouteAsync(PathString url);
    /// <summary>
    /// Get the Localization of the ContentItem from an url.
    /// </summary>
    /// <returns>Culture or null if not found.</returns>
    Task<LocalizationEntry> GetLocalizationFromRouteAsync(PathString url);
    /// <summary>
    /// Get the Localizations of the LocalizationSet from a URL.
    /// </summary>
    /// <returns>List of Localization for the url or null if not found.</returns>
    Task<IEnumerable<LocalizationEntry>> GetLocalizationsFromRouteAsync(PathString url);
    /// <summary>
    /// Sets the culture cookie to the target culture.
    /// </summary>
    void SetContentCulturePickerCookie(string targetCulture);
}
