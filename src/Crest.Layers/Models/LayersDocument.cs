using Crest.Data.Documents;

namespace Crest.Layers.Models;

public class LayersDocument : Document
{
    public List<Layer> Layers { get; set; } = [];
}
