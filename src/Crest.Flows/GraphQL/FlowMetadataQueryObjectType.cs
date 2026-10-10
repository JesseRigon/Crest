using GraphQL.Types;
using Crest.Flows.Models;

namespace Crest.Flows.GraphQL;

public class FlowMetadataQueryObjectType : ObjectGraphType<FlowMetadata>
{
    public FlowMetadataQueryObjectType()
    {
        Name = "FlowMetadata";

        Field(x => x.Size, nullable: true);
        Field<FlowAlignmentEnum>("alignment");
    }
}
