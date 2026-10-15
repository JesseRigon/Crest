using Grove.DisplayManagement;

namespace Grove.ContentTypes.Shapes;

[GenerateShape]
public partial class ContentCardFrameShape
{
    public IShape ChildContent { get; set; }
    public int? ColumnSize { get; set; }
}
