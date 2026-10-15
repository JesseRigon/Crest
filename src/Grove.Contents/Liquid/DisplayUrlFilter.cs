using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Grove.Contents.Management;
using Grove.Contents.Management.Routing;
using Grove.Display.Liquid;

namespace Grove.Contents.Liquid;

public class DisplayUrlFilter : ILiquidFilter
{
    private readonly AutorouteOptions _autorouteOptions;
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public DisplayUrlFilter(
        IOptions<AutorouteOptions> autorouteOptions,
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _autorouteOptions = autorouteOptions.Value;
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        if (_httpContextAccessor.HttpContext is not { } httpContext)
        {
            return new StringValue(string.Empty);
        }

        var contentItem = input.ToObjectValue() as ContentItem;
        RouteValueDictionary routeValues;

        if (contentItem == null)
        {
            if (string.IsNullOrEmpty(input.ToStringValue()))
            {
                return StringValue.Empty;
            }

            routeValues = new RouteValueDictionary(_autorouteOptions.GlobalRouteValues)
            {
                [_autorouteOptions.ContentItemIdKey] = input.ToStringValue(),
            };
        }
        else
        {
            var contentItemMetadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);
            routeValues = contentItemMetadata.DisplayRouteValues;
        }

        var linkUrl = _linkGenerator.GetPathByRouteValues(httpContext, string.Empty, routeValues);

        return new StringValue(linkUrl);
    }
}
