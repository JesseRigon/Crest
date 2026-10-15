using GraphQL.Types;

namespace Grove.ContentManagement.GraphQL.Queries.Types;

public interface IContentItemTypeInitializer
{
    void Initialize(ContentItemType contentItemType, ISchema schema);
}
