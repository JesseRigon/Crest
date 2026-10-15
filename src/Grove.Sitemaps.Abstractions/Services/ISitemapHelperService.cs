
namespace Grove.Sitemaps.Services;

/// <summary>
/// Helper services to provides path validation, and sitemap slugs.
/// </summary>
public interface ISitemapHelperService
{
    /// <summary>The problems with <paramref name="path"/> as the path of a sitemap (other than
    /// <paramref name="sitemapId"/>); empty when it is valid.</summary>
    Task<IReadOnlyList<string>> ValidatePathAsync(string path, string sitemapId = null);

    string GetSitemapSlug(string name);
}
