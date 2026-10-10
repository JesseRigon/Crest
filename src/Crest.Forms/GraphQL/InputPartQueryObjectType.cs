using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class InputPartQueryObjectType : ObjectGraphType<InputPart>
{
    public InputPartQueryObjectType()
    {
        Name = "InputPart";

        Field(x => x.Type, nullable: true);
        Field(x => x.Placeholder, nullable: true);
        Field(x => x.DefaultValue, nullable: true);
    }
}
