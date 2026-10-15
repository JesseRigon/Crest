using Grove.Api;
using Grove.Contents.Management;
using Grove.Search.Abstractions;
using Grove.Search.Models;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Search.Api;

/// <summary>
/// <c>api/grove/search</c>: the site search over <see cref="ISiteSearch"/>. Open to every caller;
/// the index's query permission (<c>QuerySearchIndex</c>, per index) decides who may search it.
/// </summary>
public sealed class SearchApi : ApiContributor
{
    public override string Name => "Search";

    public override string FeatureId => "Grove.Search";

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapGet("search", (HttpContext http, [FromQuery] string? index, [FromQuery] string? terms, [FromQuery] int? from, [FromQuery] int? size) =>
            ApiHandlers.For<SearchHandlers>(http).SearchAsync(index, terms, from ?? 0, size))
            .AllowAnonymous();
}

public sealed class SearchHandlers(
    ISiteSearch search,
    ISiteService siteService,
    IContentManager contentManager,
    LinkGenerator linkGenerator) : ApiHandlers
{
    public async Task<IResult> SearchAsync(string? index, string? terms, int from, int? size)
    {
        var site = await siteService.GetSiteSettingsAsync();
        var settings = site.As<SearchSettings>();
        var pageSize = Math.Clamp(size ?? site.PageSize, 1, 100);

        var result = await search.SearchAsync(index, terms, from, pageSize, User, HttpContext.RequestAborted);
        if (result.Status == SiteSearchStatus.Forbidden)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        var hits = new List<SearchHitModel>(result.Hits.Count);
        foreach (var hit in result.Hits)
        {
            var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(hit.ContentItem);
            var url = metadata.DisplayRouteValues is null
                ? null
                : linkGenerator.GetPathByRouteValues(HttpContext, string.Empty, metadata.DisplayRouteValues);

            hits.Add(new SearchHitModel(hit.ContentItem.ContentItemId, hit.ContentItem.ContentType, hit.ContentItem.DisplayText, url, [.. hit.Highlights]));
        }

        return Ok(new SearchResponse(
            result.Status.ToString(),
            result.Index,
            terms,
            settings?.PageTitle,
            settings?.Placeholder,
            [.. hits],
            Math.Max(0, from),
            pageSize,
            result.HasMore));
    }
}
