
using GraphQL.Transport;
using Grove.Data.Protocols.GraphQL.Queries;

namespace Grove.Data.Protocols.GraphQL;

public class GraphQLNamedQueryRequest : GraphQLRequest
{
    /// <summary>
    /// Used to store some graphql query on the server, and then the client only needs to submit the name of that query to reduce the size of the network request
    /// <see cref="INamedQueryProvider"/>
    /// </summary>
    public string NamedQuery { get; set; }
}
