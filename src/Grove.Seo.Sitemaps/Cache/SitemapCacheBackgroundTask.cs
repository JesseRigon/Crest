using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.Seo.Sitemaps.Services;

namespace Grove.Seo.Sitemaps.Cache;

[BackgroundTask(
    Title = "Sitemap Cache Cleaner",
    Schedule = "*/5 * * * *",
    Description = "Cleans up sitemap cache files.")]
public sealed class SitemapCacheBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var sitemapManager = serviceProvider.GetRequiredService<ISitemapManager>();
        var sitemapCacheProvider = serviceProvider.GetRequiredService<ISitemapCacheProvider>();

        var sitemaps = await sitemapManager.GetSitemapsAsync();
        await sitemapCacheProvider.CleanSitemapCacheAsync(sitemaps.Select(s => s.CacheFileName));
    }
}
