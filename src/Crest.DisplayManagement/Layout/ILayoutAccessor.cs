using Crest.DisplayManagement.Zones;

namespace Crest.DisplayManagement.Layout;

public interface ILayoutAccessor
{
    Task<IZoneHolding> GetLayoutAsync();
}
