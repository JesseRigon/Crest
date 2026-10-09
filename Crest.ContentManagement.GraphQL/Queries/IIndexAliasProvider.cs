namespace Crest.ContentManagement.GraphQL.Queries;

public interface IIndexAliasProvider
{
    ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync();
}
