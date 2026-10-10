using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class FormElementPartQueryObjectType : ObjectGraphType<FormElementPart>
{
    public FormElementPartQueryObjectType()
    {
        Name = "FormElementPart";

        Field(x => x.Id, nullable: true);
    }
}
