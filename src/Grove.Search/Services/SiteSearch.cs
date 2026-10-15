using System.Security.Claims;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Indexing;
using Grove.Indexing.Models;
using Grove.Modules;
using Grove.Search.Abstractions;
using Grove.Search.Models;
using Grove.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Services;

namespace Grove.Search.Services;

/// <summary>
/// <see cref="ISiteSearch"/>: resolves the index (the site's default when none is named), checks
/// the caller may query it, runs the engine its provider registered, loads the hits' content
/// items in the engine's order and tells the <see cref="ISearchHandler"/>s.
/// </summary>
public sealed class SiteSearch(
    ISiteService siteService,
    IIndexProfileStore indexProfiles,
    IAuthorizationService authorization,
    IServiceProvider serviceProvider,
    ISession session,
    IEnumerable<ISearchHandler> searchHandlers,
    ILogger<SiteSearch> logger) : ISiteSearch
{
    public async Task<SiteSearchResult> SearchAsync(string? indexName, string? terms, int from, int size, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        IndexProfile? index;
        if (string.IsNullOrWhiteSpace(indexName))
        {
            var settings = (await siteService.GetSiteSettingsAsync()).As<SearchSettings>();
            index = string.IsNullOrEmpty(settings?.DefaultIndexProfileName)
                ? null
                : await indexProfiles.FindByNameAsync(settings.DefaultIndexProfileName);

            if (index is null)
            {
                return new SiteSearchResult { Status = SiteSearchStatus.NoDefaultIndex };
            }
        }
        else
        {
            index = await indexProfiles.FindByNameAsync(indexName.Trim());
            if (index is null)
            {
                return new SiteSearchResult { Status = SiteSearchStatus.IndexNotFound };
            }
        }

        if (!await authorization.AuthorizeAsync(user, IndexingPermissions.QuerySearchIndex, index))
        {
            return new SiteSearchResult { Status = SiteSearchStatus.Forbidden, Index = index.Name };
        }

        var engine = serviceProvider.GetKeyedService<ISearchService>(index.ProviderName);
        if (engine is null)
        {
            return new SiteSearchResult { Status = SiteSearchStatus.NoProvider, Index = index.Name };
        }

        if (string.IsNullOrWhiteSpace(terms))
        {
            return new SiteSearchResult { Status = SiteSearchStatus.Ok, Index = index.Name };
        }

        from = Math.Max(0, from);
        size = Math.Clamp(size, 1, 100);

        // One more than the page tells whether a next page exists.
        var result = await engine.SearchAsync(index, terms, from, size + 1);
        IList<string> ids = result.Success && result.ContentItemIds is not null ? result.ContentItemIds : [];

        await searchHandlers.InvokeAsync((handler, context) => handler.SearchedAsync(context), new SearchContext
        {
            Index = index,
            Terms = terms,
            ContentItemIds = ids,
            SearchService = engine,
            TotalHits = ids.Count,
        }, logger);

        if (ids.Count == 0)
        {
            return new SiteSearchResult { Status = SiteSearchStatus.Ok, Index = index.Name };
        }

        var items = result.Latest
            ? await session.Query<ContentItem, ContentItemIndex>(item => item.ContentItemId.IsIn(ids) && item.Latest).ListAsync()
            : await session.Query<ContentItem, ContentItemIndex>(item => item.ContentItemId.IsIn(ids) && item.Published).ListAsync();

        var ordered = items
            .OrderBy(item => ids.IndexOf(item.ContentItemId))
            .ToList();

        return new SiteSearchResult
        {
            Status = SiteSearchStatus.Ok,
            Index = index.Name,
            HasMore = ordered.Count > size,
            Hits = [.. ordered.Take(size).Select(item => new SiteSearchHit(item, Highlights(result, item.ContentItemId)))],
        };
    }

    private static IReadOnlyList<string> Highlights(SearchResult result, string contentItemId) =>
        result.Highlights is not null && result.Highlights.TryGetValue(contentItemId, out var fields)
            ? [.. fields.Values.SelectMany(fragments => fragments)]
            : [];
}
