using Grove.Data.Documents;
using Grove.DisplayManagement.Descriptors.ShapePlacementStrategy;

namespace Grove.Placements.Models;

public class PlacementsDocument : Document
{
    public Dictionary<string, PlacementNode[]> Placements { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
