using System.Net;
using System.Text;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Search.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Connectors.Consumers.Tests;

/// <summary>The SDK clients' transport: every request is one engine call on the connection.</summary>
public sealed class EngineRoutedHttpHandlerTests
{
    [Fact]
    public async Task ARequestGoesThroughTheEngineOnTheConnectionAsTheSystemActor()
    {
        var (engine, stub, client) = Build(new ConnectionPolicy(), (200, """{"hits":[]}"""));

        using var response = await client.PostAsync("https://es.example:9200/index/_search", new StringContent("""{"query":{}}""", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"hits":[]}""", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var (key, caller, options) = Assert.Single(engine.SdkCalls);
        Assert.Equal("es", key);
        Assert.True(caller.IsSystem);
        Assert.Equal(CredentialScope.Tenant, options!.Scope);
        Assert.False(options.IsSafeToRetry);
        Assert.Equal("""{"query":{}}""", Assert.Single(stub.Requests).Body);
    }

    [Fact]
    public async Task ATransientAnswerIsRetriedByTheEnginesPolicyNotTheSdk()
    {
        var (_, stub, client) = Build(new ConnectionPolicy { RetryCount = 2 }, (503, "busy"), (200, "ok"));

        using var response = await client.GetAsync("https://es.example:9200/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task AFinalAnswerReachesTheSdkAsItCame()
    {
        var (_, stub, client) = Build(new ConnectionPolicy(), (404, "missing"));

        using var response = await client.GetAsync("https://es.example:9200/nope", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("missing", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task RetriesExhaustedHandTheLastAnswerBack()
    {
        var (_, stub, client) = Build(new ConnectionPolicy { RetryCount = 1 }, (503, "busy"));

        using var response = await client.GetAsync("https://es.example:9200/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task AnAnswerOverTheConnectionsCapIsRefused()
    {
        var (_, _, client) = Build(new ConnectionPolicy { MaxResponseBytes = 4 }, (200, "too large"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://es.example:9200/", TestContext.Current.CancellationToken));
    }

    private static (FakeConnectorEngine Engine, StubHttpHandler Stub, HttpClient Client) Build(ConnectionPolicy policy, params (int, string)[] answers)
    {
        var stub = new StubHttpHandler(answers);
        var engine = new FakeConnectorEngine { SdkHttp = stub };
        engine.SdkConnections["es"] = (new ConnectionDefinition { Key = "es", DisplayName = "es", Protocol = ConnectorProtocols.Sdk, Policy = policy }, null);
        var services = new ServiceCollection()
            .AddSingleton<IConnectorEngine>(engine)
            .AddSingleton<ICallerContextFactory>(new FakeCallerFactory())
            .BuildServiceProvider();

        return (engine, stub, new HttpClient(new EngineRoutedHttpHandler(services, "es")));
    }
}
