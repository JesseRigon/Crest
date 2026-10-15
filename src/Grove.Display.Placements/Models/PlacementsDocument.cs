using Grove.Data.Documents;
using Grove.Display.Management.Descriptors.ShapePlacementStrategy;

namespace Grove.Display.Placements.Models;

public class PlacementsDocument : Document
{
    public Dictionary<string, PlacementNode[]> Placements { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
