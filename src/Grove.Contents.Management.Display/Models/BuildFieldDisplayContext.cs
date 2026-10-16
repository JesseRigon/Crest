using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Handlers;

namespace Grove.Contents.Management.Display.Models;

public class BuildFieldDisplayContext : BuildDisplayContext
{
    public BuildFieldDisplayContext(ContentFieldSet contentFieldSet, ContentTypeFieldSetDefinition typeFieldSetDefinition, ContentFieldDefinition partFieldDefinition, BuildDisplayContext context)
        : base(context.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout)
    {
        ContentFieldSet = contentFieldSet;
        TypeFieldSetDefinition = typeFieldSetDefinition;
        FieldDefinition = partFieldDefinition;
    }

    public ContentFieldSet ContentFieldSet { get; }
    public ContentTypeFieldSetDefinition TypeFieldSetDefinition { get; }
    public ContentFieldDefinition FieldDefinition { get; }
}
