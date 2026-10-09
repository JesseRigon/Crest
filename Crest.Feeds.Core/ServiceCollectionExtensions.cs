using Microsoft.Extensions.DependencyInjection;
using Crest.Feeds.Rss;

namespace Crest.Feeds;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeeds(this IServiceCollection services)
    {
        services.AddSingleton<IFeedBuilderProvider, RssFeedBuilderProvider>();

        return services;
    }
}
