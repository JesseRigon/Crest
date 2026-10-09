using Crest.Autoroute.Core.Indexes;
using Crest.ContentManagement.GraphQL.Queries;

namespace Crest.Autoroute.GraphQL;

public class AutoroutePartIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "autoroutePart",
            Index = nameof(AutoroutePartIndex),
            IndexType = typeof(AutoroutePartIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
