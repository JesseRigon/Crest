using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class ButtonPartQueryObjectType : ObjectGraphType<ButtonPart>
{
    public ButtonPartQueryObjectType()
    {
        Name = "ButtonPart";

        Field(x => x.Text, nullable: true);
        Field(x => x.Type, nullable: true);
    }
}
