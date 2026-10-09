using GraphQL.Types;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.ContentManagement.Metadata.Models;
using Crest.Flows.Models;

namespace Crest.Flows.GraphQL;

public class FlowMetadataContentTypeBuilder : IContentTypeBuilder
{
    public void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        if (contentTypeDefinition.GetStereotype() != "Widget")
        {
            return;
        }

        contentItemType.Field<FlowMetadataQueryObjectType>("metadata")
            .Resolve(context => context.Source.TryGet<FlowMetadata>(out var flowMetadata) ? flowMetadata : null);
    }

    public void Clear()
    {
    }
}
