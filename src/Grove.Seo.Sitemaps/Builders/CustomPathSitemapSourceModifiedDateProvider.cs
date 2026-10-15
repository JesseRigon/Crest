using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Builders;

public class CustomPathSitemapSourceModifiedDateProvider : SitemapSourceModifiedDateProviderBase<CustomPathSitemapSource>
{
    public override Task<DateTime?> GetLastModifiedDateAsync(CustomPathSitemapSource source)
    {
        return Task.FromResult(source.LastUpdate);
    }
}
