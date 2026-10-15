using System.Net.Mime;
using Grove.Api;
using Grove.Data.Feeds.Models;
using Grove.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Data.Feeds.Api;

/// <summary>
/// The feeds (docs/MVC/api.md › P11): each path a module declares in <see cref="FeedRoutesOptions"/>
/// (the list feed <c>Contents/Lists/{contentItemId}/rss</c>), anonymous <c>GET</c>, answering the
/// feed document (<c>text/xml</c>) or 404 when no builder answers the format or no query matches.
/// </summary>
public sealed class FeedsApi(IOptions<FeedRoutesOptions> options) : ApiContributor
{
    public override string Name => "Feeds";

    public override string FeatureId => "Grove.Data.Feeds";

    public override string RoutePrefix => "";

    public override void Map(IEndpointRouteBuilder group)
    {
        foreach (var route in options.Value.Routes)
        {
            var format = route.Format;
            group.MapGet(route.Pattern, ApiHandlers.Handle<FeedHandlers>(h => h.RenderAsync(format)))
                .AllowAnonymous()
                .ExcludeFromDescription();
        }
    }
}

public sealed class FeedHandlers(
    IEnumerable<IFeedQueryProvider> feedQueryProviders,
    IEnumerable<IFeedBuilderProvider> feedFormatProviders,
    IFeedItemBuilder feedItemBuilder,
    IShellLinkGenerator links,
    IServiceProvider serviceProvider) : ApiHandlers
{
    public async Task<IResult> RenderAsync(string format)
    {
        var context = new FeedContext(HttpContext, format);

        var bestFormatterMatch = feedFormatProviders
            .Select(provider => provider.Match(context))
            .Where(match => match != null && match.FeedBuilder != null)
            .MaxBy(match => match.Priority);

        if (bestFormatterMatch == null || bestFormatterMatch.FeedBuilder == null)
        {
            return NotFound();
        }

        context.Builder = bestFormatterMatch.FeedBuilder;

        var queryMatches = new List<FeedQueryMatch>();
        foreach (var provider in feedQueryProviders)
        {
            queryMatches.Add(await provider.MatchAsync(context));
        }

        var bestQueryMatch = queryMatches
            .Where(match => match != null && match.FeedQuery != null)
            .MaxBy(match => match.Priority);

        if (bestQueryMatch == null || bestQueryMatch.FeedQuery == null)
        {
            return NotFound();
        }

        var document = await context.Builder.ProcessAsync(context, async () =>
        {
            await bestQueryMatch.FeedQuery.ExecuteAsync(context);
            await feedItemBuilder.PopulateAsync(context);

            var contextualizeContext = new ContextualizeContext
            {
                ServiceProvider = serviceProvider,
                Links = links,
            };

            foreach (var contextualizer in context.Response.Contextualizers)
            {
                contextualizer(contextualizeContext);
            }
        });

        return Results.Content(document.ToString(), MediaTypeNames.Text.Xml);
    }
}
