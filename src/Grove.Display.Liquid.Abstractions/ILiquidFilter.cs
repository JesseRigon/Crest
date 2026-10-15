using Fluid;
using Fluid.Values;

namespace Grove.Display.Liquid;

public interface ILiquidFilter
{
    ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context);
}
