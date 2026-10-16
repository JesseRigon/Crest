using Grove.Contents.Localization.Records;
using Grove.Contents.Management.GraphQL.Queries;

namespace Grove.Contents.Localization.GraphQL;

public class LocalizationIndexAliasProvider : IIndexAliasProvider
{
    private static readonly IndexAlias[] s_aliases =
    [
        new IndexAlias
        {
            Alias = "localization",
            Index = nameof(LocalizedContentItemIndex),
            IndexType = typeof(LocalizedContentItemIndex),
        }
    ];

    public ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return ValueTask.FromResult<IEnumerable<IndexAlias>>(s_aliases);
    }
}
