using Fluid;
using Fluid.Values;

namespace Crest.Liquid;

public interface ILiquidFilter
{
    ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context);
}
