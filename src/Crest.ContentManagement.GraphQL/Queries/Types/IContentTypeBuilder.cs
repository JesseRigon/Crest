using GraphQL.Types;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentManagement.GraphQL.Queries.Types;

public interface IContentTypeBuilder
{
    void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType);

    void Clear();
}
