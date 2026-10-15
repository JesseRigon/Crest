using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentManagement.Display.Models;

public class BuildFieldDisplayContext : BuildDisplayContext
{
    public BuildFieldDisplayContext(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, ContentPartFieldDefinition partFieldDefinition, BuildDisplayContext context)
        : base(context.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout, context.Updater)
    {
        ContentPart = contentPart;
        TypePartDefinition = typePartDefinition;
        PartFieldDefinition = partFieldDefinition;
    }

    public ContentPart ContentPart { get; }
    public ContentTypePartDefinition TypePartDefinition { get; }
    public ContentPartFieldDefinition PartFieldDefinition { get; }
}
