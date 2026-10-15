using Fluid;
using Fluid.Values;
using Grove.Routing;

namespace Grove.Display.Liquid.Filters;

/// <summary><c>href</c>: an app-relative path (<c>~/...</c>) as a path under the tenant's base.</summary>
public class ContentUrlFilter : ILiquidFilter
{
    private readonly IShellLinkGenerator _links;

    public ContentUrlFilter(IShellLinkGenerator links)
    {
        _links = links;
    }

    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
        => ValueTask.FromResult<FluidValue>(new StringValue(_links.Content(input.ToStringValue().Trim())));
}
