using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class LabelPartQueryObjectType : ObjectGraphType<LabelPart>
{
    public LabelPartQueryObjectType()
    {
        Name = "LabelPart";

        Field(x => x.For, nullable: true);
        Field("value", context => context.ContentItem.DisplayText);
    }
}
