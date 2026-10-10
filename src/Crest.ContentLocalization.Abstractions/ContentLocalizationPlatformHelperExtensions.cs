using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.ContentLocalization;
using Crest.ContentManagement;

/// <summary>
/// Provides an extension methods for <see cref="IPlatformHelper"/>.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public static class ContentLocalizationPlatformHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Gets the culture for a given <see cref="ContentItem"/>.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="contentItem">The <see cref="ContentItem"/> in which to get its culture.</param>
    /// <returns></returns>
    public static async Task<CultureInfo> GetContentCultureAsync(this IPlatformHelper platformHelper, ContentItem contentItem)
    {
        var contentManager = platformHelper.HttpContext.RequestServices.GetService<IContentManager>();
        var cultureAspect = await contentManager.PopulateAspectAsync(contentItem, new CultureAspect());

        return cultureAspect.Culture;
    }
}
