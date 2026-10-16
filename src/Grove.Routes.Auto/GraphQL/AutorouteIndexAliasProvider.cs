using Grove.Routes.Auto.Indexes;
using Grove.Contents.Management.GraphQL.Queries;

namespace Grove.Routes.Auto.GraphQL;

public class AutorouteIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "autoroute",
            Index = nameof(AutorouteIndex),
            IndexType = typeof(AutorouteIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
