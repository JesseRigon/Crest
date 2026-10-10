using Crest.ContentManagement.GraphQL.Queries;
using Crest.Lists.Indexes;

namespace Crest.Lists.GraphQL;

public class ContainedPartIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "containedPart",
            Index = nameof(ContainedPartIndex),
            IndexType = typeof(ContainedPartIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
