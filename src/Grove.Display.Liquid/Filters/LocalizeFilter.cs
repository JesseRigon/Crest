using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;

namespace Grove.Display.Liquid.Filters;

/// <summary><c>t</c>: the input translated through the platform's string localizer, arguments filling its placeholders.</summary>
public sealed class LocalizeFilter : ILiquidFilter
{
    private readonly IStringLocalizer S;

    public LocalizeFilter(IStringLocalizer<LocalizeFilter> localizer)
    {
        S = localizer;
    }

    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        var parameters = new object[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            parameters[i] = arguments.At(i).ToStringValue();
        }

        return ValueTask.FromResult<FluidValue>(new StringValue(S[input.ToStringValue(), parameters]));
    }
}
