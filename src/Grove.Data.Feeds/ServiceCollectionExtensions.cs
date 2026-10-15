using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Feeds.Rss;

namespace Grove.Data.Feeds;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeeds(this IServiceCollection services)
    {
        services.AddSingleton<IFeedBuilderProvider, RssFeedBuilderProvider>();

        return services;
    }
}
