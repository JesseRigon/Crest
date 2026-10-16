using Grove.Contents.Alias.Indexes;
using Grove.Contents.Management.GraphQL.Queries;

namespace Grove.Contents.Alias.GraphQL;

public class AliasIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "alias",
            Index = "AliasIndex",
            IndexType = typeof(AliasIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
