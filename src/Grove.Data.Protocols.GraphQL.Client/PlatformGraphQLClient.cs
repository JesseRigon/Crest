namespace Grove.Data.Protocols.GraphQL.Client;

public class PlatformGraphQLClient
{
    public PlatformGraphQLClient(HttpClient client) => Client = client;

    public ContentResource Content => new(Client);

    public HttpClient Client { get; }
}
