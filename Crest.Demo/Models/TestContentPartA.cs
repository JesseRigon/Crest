using Crest.ContentManagement;
using Crest.DisplayManagement.Shapes;

namespace Crest.Demo.Models;

public class TestContentPartA : ContentPart
{
    public ShapeMetadata Metadata { get; set; }
    public string Line { get; set; }
}
