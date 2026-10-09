using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Routing;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Views;
using Crest.Navigation;
using Crest.Taxonomies.Core;
using Crest.Taxonomies.Models;
using Crest.Taxonomies.ViewModels;

namespace Crest.Taxonomies.Drivers;

public sealed class TermPartContentDriver : ContentDisplayDriver
{
    private readonly IContentsTaxonomyListQueryService _contentsTaxonomyListQueryService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AutorouteOptions _autorouteOptions;
    private readonly PagerOptions _pagerOptions;
    private readonly IContentManager _contentManager;

    public TermPartContentDriver(
        IOptions<PagerOptions> pagerOptions,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AutorouteOptions> autorouteOptions,
        IContentsTaxonomyListQueryService contentsTaxonomyListQueryService,
        IContentManager contentManager)
    {
        _contentsTaxonomyListQueryService = contentsTaxonomyListQueryService;
        _httpContextAccessor = httpContextAccessor;
        _autorouteOptions = autorouteOptions.Value;
        _pagerOptions = pagerOptions.Value;
        _contentManager = contentManager;
    }

    public override IDisplayResult Display(ContentItem model, BuildDisplayContext context)
    {
        if (!model.TryGet<TermPart>(out var part))
        {
            return null;
        }

        return Initialize<TermPartViewModel>("TermPart", async m =>
        {
            var pager = await GetPagerAsync(context.Updater, _pagerOptions.GetPageSize());

            var (totalItemCount, containedItems) = await QueryTermItemsAsync(part, pager);
            m.TaxonomyContentItemId = part.TaxonomyContentItemId;
            m.ContentItem = part.ContentItem;
            m.ContentItems = containedItems;

            var routeValues = new RouteValueDictionary(_autorouteOptions.GlobalRouteValues);

            routeValues[_autorouteOptions.ContentItemIdKey] = part.ContentItem.ContentItemId;

            if (_httpContextAccessor.HttpContext?.Request?.RouteValues is not null &&
            _httpContextAccessor.HttpContext.Request.RouteValues.TryGetValue(_autorouteOptions.JsonPathKey, out var jsonPath))
            {
                routeValues[_autorouteOptions.JsonPathKey] = jsonPath;
            }

            m.Pager = await context.ShapeFactory.PagerAsync(pager, totalItemCount, routeValues);
        }).Location(PlatformConstants.DisplayType.Detail, "Content:5");
    }

    private async Task<(int, IEnumerable<ContentItem>)> QueryTermItemsAsync(TermPart termPart, Pager pager)
    {
        var query = await _contentsTaxonomyListQueryService.QueryAsync(termPart, pager);

        var totalItems = await query.CountAsync();

        var containedItems = await query.ListAsync();

        await _contentManager.LoadAsync(containedItems);

        return (totalItems, containedItems);
    }

    private static async Task<Pager> GetPagerAsync(IUpdateModel updater, int pageSize)
    {
        var pagerParameters = new PagerParameters();

        await updater.TryUpdateModelAsync(pagerParameters);

        var pager = new Pager(pagerParameters, pageSize);

        return pager;
    }
}
