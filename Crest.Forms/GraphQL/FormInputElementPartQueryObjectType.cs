using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class FormInputElementPartQueryObjectType : ObjectGraphType<FormInputElementPart>
{
    public FormInputElementPartQueryObjectType()
    {
        Name = "FormInputElementPart";

        Field(x => x.Name, nullable: true);
    }
}
