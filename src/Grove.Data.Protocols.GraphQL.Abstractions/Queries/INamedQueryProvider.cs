namespace Grove.Data.Protocols.GraphQL.Queries;

public interface INamedQueryProvider
{
    IDictionary<string, string> Resolve();
}
