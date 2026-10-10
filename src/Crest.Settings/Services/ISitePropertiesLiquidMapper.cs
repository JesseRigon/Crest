using Fluid.Values;

namespace Crest.Settings.Services;

public interface ISitePropertiesLiquidMapper
{
    Task<FluidValue> MapAsync(ISite site);
}
