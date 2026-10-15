using Fluid.Values;

namespace Grove.Infrastructure.Settings.Services;

public interface ISitePropertiesLiquidMapper
{
    Task<FluidValue> MapAsync(ISite site);
}
