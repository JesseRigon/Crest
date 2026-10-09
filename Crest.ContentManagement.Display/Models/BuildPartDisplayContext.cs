using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentManagement.Display.Models;

public class BuildPartDisplayContext : BuildDisplayContext
{
    public BuildPartDisplayContext(ContentTypePartDefinition typePartDefinition, BuildDisplayContext context)
        : base(context.Shape, context.DisplayType, context.GroupId, context.ShapeFactory, context.Layout, context.Updater)
    {
        TypePartDefinition = typePartDefinition;
    }

    public ContentTypePartDefinition TypePartDefinition { get; }
}
