using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Handlers;

namespace Grove.Contents.Management.Display.Models;

public class BuildFieldSetDisplayContext : BuildDisplayContext
{
    public BuildFieldSetDisplayContext(ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDisplayContext context)
        : base(context.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout)
    {
        TypeFieldSetDefinition = typeFieldSetDefinition;
    }

    public ContentTypeFieldSetDefinition TypeFieldSetDefinition { get; }
}
