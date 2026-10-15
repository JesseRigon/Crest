using Grove.DisplayManagement.Zones;

namespace Grove.DisplayManagement.Layout;

public interface ILayoutAccessor
{
    Task<IZoneHolding> GetLayoutAsync();
}
