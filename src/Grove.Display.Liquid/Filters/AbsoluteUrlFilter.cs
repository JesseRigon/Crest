using Fluid;
using Fluid.Values;
using Grove.Routing;

namespace Grove.Display.Liquid.Filters;

/// <summary><c>absolute_url</c>: a path (relative, app-relative or rooted) as an absolute URL of the tenant.</summary>
public class AbsoluteUrlFilter : ILiquidFilter
{
    private readonly IShellLinkGenerator _links;

    public AbsoluteUrlFilter(IShellLinkGenerator links)
    {
        _links = links;
    }

    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        var relativePath = input.ToStringValue();

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return ValueTask.FromResult(input);
        }

        return ValueTask.FromResult<FluidValue>(StringValue.Create(_links.ToAbsoluteUrl(relativePath)));
    }
}
