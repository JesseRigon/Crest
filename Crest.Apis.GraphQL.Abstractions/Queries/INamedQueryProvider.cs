namespace Crest.Apis.GraphQL.Queries;

public interface INamedQueryProvider
{
    IDictionary<string, string> Resolve();
}
