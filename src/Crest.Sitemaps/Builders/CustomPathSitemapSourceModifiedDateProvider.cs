using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Builders;

public class CustomPathSitemapSourceModifiedDateProvider : SitemapSourceModifiedDateProviderBase<CustomPathSitemapSource>
{
    public override Task<DateTime?> GetLastModifiedDateAsync(CustomPathSitemapSource source)
    {
        return Task.FromResult(source.LastUpdate);
    }
}
