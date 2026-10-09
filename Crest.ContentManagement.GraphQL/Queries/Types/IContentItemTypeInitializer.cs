using GraphQL.Types;

namespace Crest.ContentManagement.GraphQL.Queries.Types;

public interface IContentItemTypeInitializer
{
    void Initialize(ContentItemType contentItemType, ISchema schema);
}
