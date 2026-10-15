using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Grove.Contents.Localization.Api;
using Grove.Display.Liquid;
using Grove.Routing;

namespace Grove.Contents.Localization.Liquid;

/// <summary><c>switch_culture_url</c>: the culture redirect endpoint's URL for the current page and a target culture.</summary>
public class SwitchCultureUrlFilter : ILiquidFilter
{
    private readonly IShellLinkGenerator _links;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SwitchCultureUrlFilter(IShellLinkGenerator links, IHttpContextAccessor httpContextAccessor)
    {
        _links = links;
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new ArgumentException("HttpRequest missing while invoking 'switch_culture_url'");

        var url = _links.GetPathByName(CultureRedirectApi.RouteName, new RouteValueDictionary
        {
            ["targetCulture"] = input.ToStringValue(),
            ["contentItemUrl"] = request.Path.Value,
            ["queryStringValue"] = request.QueryString.Value,
        });

        return ValueTask.FromResult(FluidValue.Create(url, context.Options));
    }
}
