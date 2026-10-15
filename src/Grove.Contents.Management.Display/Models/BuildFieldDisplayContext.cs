using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Handlers;

namespace Grove.Contents.Management.Display.Models;

public class BuildFieldDisplayContext : BuildDisplayContext
{
    public BuildFieldDisplayContext(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, ContentPartFieldDefinition partFieldDefinition, BuildDisplayContext context)
        : base(context.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout)
    {
        ContentPart = contentPart;
        TypePartDefinition = typePartDefinition;
        PartFieldDefinition = partFieldDefinition;
    }

    public ContentPart ContentPart { get; }
    public ContentTypePartDefinition TypePartDefinition { get; }
    public ContentPartFieldDefinition PartFieldDefinition { get; }
}
