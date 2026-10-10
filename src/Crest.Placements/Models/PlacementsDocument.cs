using Crest.Data.Documents;
using Crest.DisplayManagement.Descriptors.ShapePlacementStrategy;

namespace Crest.Placements.Models;

public class PlacementsDocument : Document
{
    public Dictionary<string, PlacementNode[]> Placements { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
