using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Handlers;

namespace Grove.Contents.Management.Display.Models;

public class BuildPartDisplayContext : BuildDisplayContext
{
    public BuildPartDisplayContext(ContentTypePartDefinition typePartDefinition, BuildDisplayContext context)
        : base(context.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout)
    {
        TypePartDefinition = typePartDefinition;
    }

    public ContentTypePartDefinition TypePartDefinition { get; }
}
