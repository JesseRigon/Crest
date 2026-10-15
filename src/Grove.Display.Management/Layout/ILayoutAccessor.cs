using Grove.Display.Management.Zones;

namespace Grove.Display.Management.Layout;

public interface ILayoutAccessor
{
    Task<IZoneHolding> GetLayoutAsync();
}
