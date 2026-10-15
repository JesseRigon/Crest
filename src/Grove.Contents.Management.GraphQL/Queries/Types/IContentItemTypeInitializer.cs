using GraphQL.Types;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public interface IContentItemTypeInitializer
{
    void Initialize(ContentItemType contentItemType, ISchema schema);
}
