using Crest.Layers.Models;

namespace Crest.Layers.ViewModels;

public class LayerMetadataEditViewModel
{
    public string Title { get; set; }
    public LayerMetadata LayerMetadata { get; set; }
    public List<Layer> Layers { get; set; }
}
