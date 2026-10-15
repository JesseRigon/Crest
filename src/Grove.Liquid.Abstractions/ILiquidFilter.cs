using Fluid;
using Fluid.Values;

namespace Grove.Liquid;

public interface ILiquidFilter
{
    ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context);
}
