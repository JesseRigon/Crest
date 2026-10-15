using Microsoft.Extensions.DependencyInjection;
using Grove.Feeds.Rss;

namespace Grove.Feeds;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeeds(this IServiceCollection services)
    {
        services.AddSingleton<IFeedBuilderProvider, RssFeedBuilderProvider>();

        return services;
    }
}
