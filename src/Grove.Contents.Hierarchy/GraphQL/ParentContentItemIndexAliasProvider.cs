using Grove.Contents.Management.GraphQL.Queries;
using Grove.Contents.Hierarchy.Indexes;

namespace Grove.Contents.Hierarchy.GraphQL;

public class ParentContentItemIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "parentContentItem",
            Index = nameof(ParentContentItemIndex),
            IndexType = typeof(ParentContentItemIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
