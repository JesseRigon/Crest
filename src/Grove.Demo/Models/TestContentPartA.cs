using Grove.ContentManagement;
using Grove.DisplayManagement.Shapes;

namespace Grove.Demo.Models;

public class TestContentPartA : ContentPart
{
    public ShapeMetadata Metadata { get; set; }
    public string Line { get; set; }
}
