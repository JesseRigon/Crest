using Crest.DisplayManagement.ModelBinding;

namespace Crest.Sitemaps.Services;

/// <summary>
/// Helper services to provides path validation, and sitemap slugs.
/// </summary>
public interface ISitemapHelperService
{
    Task ValidatePathAsync(string path, IUpdateModel updater, string sitemapId = null);

    string GetSitemapSlug(string name);
}
