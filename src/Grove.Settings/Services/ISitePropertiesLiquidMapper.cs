using Fluid.Values;

namespace Grove.Settings.Services;

public interface ISitePropertiesLiquidMapper
{
    Task<FluidValue> MapAsync(ISite site);
}
