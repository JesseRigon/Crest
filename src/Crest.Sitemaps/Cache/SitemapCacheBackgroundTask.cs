using Microsoft.Extensions.DependencyInjection;
using Crest.BackgroundTasks;
using Crest.Sitemaps.Services;

namespace Crest.Sitemaps.Cache;

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
