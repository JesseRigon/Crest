using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class FormPartQueryObjectType : ObjectGraphType<FormPart>
{
    public FormPartQueryObjectType()
    {
        Name = "FormPart";

        Field(x => x.WorkflowTypeId, nullable: true);
        Field(x => x.Action, nullable: true);
        Field(x => x.Method, nullable: true);
    }
}
