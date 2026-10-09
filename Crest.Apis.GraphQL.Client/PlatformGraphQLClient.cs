namespace Crest.Apis.GraphQL.Client;

public class PlatformGraphQLClient
{
    public PlatformGraphQLClient(HttpClient client) => Client = client;

    public ContentResource Content => new(Client);

    public HttpClient Client { get; }
}
