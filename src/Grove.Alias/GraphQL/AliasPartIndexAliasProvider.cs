using Grove.Alias.Indexes;
using Grove.ContentManagement.GraphQL.Queries;

namespace Grove.Alias.GraphQL;

public class AliasPartIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "aliasPart",
            Index = "AliasPartIndex",
            IndexType = typeof(AliasPartIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
