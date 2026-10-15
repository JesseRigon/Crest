namespace Grove.Contents.Management.GraphQL.Queries;

public interface IIndexAliasProvider
{
    ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync();
}
