using GraphQL.Types;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentManagement.GraphQL.Queries.Types;

public interface IContentTypeBuilder
{
    void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType);

    void Clear();
}
