using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class ValidationPartQueryObjectType : ObjectGraphType<ValidationPart>
{
    public ValidationPartQueryObjectType()
    {
        Name = "ValidationPart";

        Field(x => x.For, nullable: true);
    }
}
