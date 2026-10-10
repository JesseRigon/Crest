using GraphQL.Types;
using Crest.Forms.Models;

namespace Crest.Forms.GraphQL;

public class ValidationSummaryPartQueryObjectType : ObjectGraphType<ValidationSummaryPart>
{
    public ValidationSummaryPartQueryObjectType()
    {
        Name = "ValidationSummaryPart";
    }
}
