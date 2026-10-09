using Crest.ContentManagement;

namespace Crest.Layers.Models;

public class LayerMetadata : ContentPart
{
    public bool RenderTitle { get; set; }
    public double Position { get; set; }
    public string Zone { get; set; }
    public string Layer { get; set; }
}
