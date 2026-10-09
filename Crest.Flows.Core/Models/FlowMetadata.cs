using Crest.ContentManagement;

namespace Crest.Flows.Models;

public enum FlowAlignment
{
    Left,
    Center,
    Right,
    Justify,
    Inherit,
}

public class FlowMetadata : ContentPart
{
    public FlowAlignment Alignment { get; set; } = FlowAlignment.Justify;
    public int Size { get; set; } = 100;
}
