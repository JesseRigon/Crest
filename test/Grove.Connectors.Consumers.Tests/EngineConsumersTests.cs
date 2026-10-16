using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Environment.Shell.Configuration;
using Grove.Infrastructure.Settings;
using Grove.Integrations.AI.Azure.Models;
using Grove.Integrations.AI.Azure.Services;
using Grove.Integrations.Facebook;
using Grove.Integrations.Facebook.Connections;
using Grove.Integrations.Facebook.Models;
using Grove.Integrations.Facebook.Services;
using Grove.Integrations.Facebook.Settings;
using Grove.Search.Elasticsearch.Models;
using Grove.Search.Elasticsearch.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grove.Connectors.Consumers.Tests;

public sealed class EngineConsumersTests
{
    [Fact]
    public async Task MetaConversionEventsGoThroughTheConversionsConnection()
    {
        var engine = new FakeConnectorEngine();
        var service = Conversions(engine, new FacebookPixelSettings { PixelId = "123", ConversionsApiTestEventCode = "TEST1" });

        var result = await service.SendEventAsync(new MetaConversionEvent { EventName = "Purchase", EventId = "order-7", EventSourceUrl = "https://shop.example/thanks" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var call = Assert.Single(engine.Calls);
        Assert.Equal(FacebookConstants.ConversionsConnectionKey, call.ConnectionKey);
        Assert.Equal(FacebookConstants.Operations.SendEvents, call.OperationId);
        Assert.Equal(CredentialScope.Tenant, call.Scope);
        Assert.Same(FakeCallerFactory.System, call.Caller);
        Assert.Equal("order-7", call.IdempotencyKey);
        Assert.Equal("123", call.Arguments!["pixelId"]!.GetValue<string>());
        Assert.Equal("TEST1", call.Arguments!["test_event_code"]!.GetValue<string>());
        var data = Assert.Single(call.Arguments!["data"]!.AsArray())!;
        Assert.Equal("Purchase", data["event_name"]!.GetValue<string>());
        Assert.Equal("website", data["action_source"]!.GetValue<string>());
        Assert.Equal("https://shop.example/thanks", data["event_source_url"]!.GetValue<string>());
    }

    [Fact]
    public async Task MetaConversionEventsFailWithoutPixelOrToken()
    {
        var engine = new FakeConnectorEngine { Answer = _ => new ConnectorResult { Outcome = ConnectorOutcome.NotAuthorized } };

        Assert.False((await Conversions(engine, new FacebookPixelSettings()).SendEventAsync(new MetaConversionEvent { EventName = "Lead" }, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Empty(engine.Calls);

        Assert.False((await Conversions(engine, new FacebookPixelSettings { PixelId = "1" }).SendEventAsync(new MetaConversionEvent { EventName = "Lead" }, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Single(engine.Calls);

        Assert.False((await Conversions(engine, new FacebookPixelSettings { PixelId = "1" }).SendEventAsync(new MetaConversionEvent(), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public void TheConversionsConnectionCarriesTheDatasetTokenAsAQueryKey()
    {
        var services = new ServiceCollection().AddConversionsConnection().BuildServiceProvider();
        var connection = Assert.Single(services.GetServices<ConnectionDeclaration>()).Definition;

        Assert.Equal(AuthSchemes.ApiKey, connection.Auth.Scheme);
        Assert.Equal("access_token", connection.Auth.KeyName);
        Assert.Equal(ApiKeyLocation.Query, connection.Auth.KeyLocation);
    }

    [Fact]
    public void AzureAISearchTakesItsEndpointAndKeyFromTheConnection()
    {
        var engine = new FakeConnectorEngine();
        engine.SdkConnections["search"] = (
            new ConnectionDefinition { Key = "search", DisplayName = "Search", Protocol = ConnectorProtocols.Sdk, BaseUrl = "https://grove.search.windows.net" },
            new ConnectionCredential { Values = new Dictionary<string, string> { [CredentialKeys.ApiKey] = "k1" } });
        var configuration = new AzureAISearchDefaultOptionsConfigurations(
            Shell(new() { ["Grove_AzureAISearch:IndexesPrefix"] = "dev" }),
            Sites.With(new JsonObject { [nameof(AzureAISearchDefaultSettings)] = new JsonObject { ["Connection"] = "search" } }),
            Engine(engine),
            NullLogger<AzureAISearchDefaultOptionsConfigurations>.Instance);

        var options = new AzureAISearchDefaultOptions();
        configuration.Configure(options);

        Assert.True(options.ConfigurationExists());
        Assert.Equal("https://grove.search.windows.net", options.Endpoint);
        Assert.Equal(AzureAIAuthenticationType.ApiKey, options.AuthenticationType);
        Assert.Equal("k1", options.Credential!.Key);
        Assert.Equal("dev", options.IndexesPrefix);
        var (key, caller, sdk) = Assert.Single(engine.SdkCalls);
        Assert.Equal("search", key);
        Assert.True(caller.IsSystem);
        Assert.Equal(CredentialScope.Tenant, sdk!.Scope);
    }

    [Fact]
    public void AzureAISearchUsesAManagedIdentityWhenTheConnectionHasNoKey()
    {
        var options = AzureAISearchDefaultOptionsConfigurations.Apply(
            new ConnectionDefinition
            {
                Key = "azure-ai-search",
                DisplayName = "Search",
                Protocol = ConnectorProtocols.Sdk,
                BaseUrl = "https://x.search.windows.net",
                Settings = new Dictionary<string, string> { [AzureAISearchDefaultOptionsConfigurations.IdentityClientIdSetting] = "mi-1" },
            },
            null);

        Assert.Equal(AzureAIAuthenticationType.ManagedIdentity, options.AuthenticationType);
        Assert.Equal("mi-1", options.IdentityClientId);
        Assert.Null(options.Credential);
    }

    [Fact]
    public void AzureAISearchIsNotConfiguredWithoutItsConnection()
    {
        var configuration = new AzureAISearchDefaultOptionsConfigurations(Shell([]), Sites.With([]), Engine(new FakeConnectorEngine()), NullLogger<AzureAISearchDefaultOptionsConfigurations>.Instance);

        var options = new AzureAISearchDefaultOptions();
        configuration.Configure(options);

        Assert.False(options.ConfigurationExists());
    }

    [Fact]
    public void ElasticsearchTakesItsNodeAndCredentialFromTheConnection()
    {
        var engine = new FakeConnectorEngine();
        engine.SdkConnections["elasticsearch"] = (
            new ConnectionDefinition
            {
                Key = "elasticsearch",
                DisplayName = "Elasticsearch",
                Protocol = ConnectorProtocols.Sdk,
                BaseUrl = "https://es.example:9243",
                Settings = new Dictionary<string, string> { [ElasticsearchConnectionOptionsConfigurations.CertificateFingerprintSetting] = "AA:BB" },
            },
            new ConnectionCredential { Values = new Dictionary<string, string> { [CredentialKeys.Username] = "elastic", [CredentialKeys.Password] = "pw" } });
        var configuration = new ElasticsearchConnectionOptionsConfigurations(Shell([]), Engine(engine), NullLogger<ElasticsearchConnectionOptionsConfigurations>.Instance);

        var options = new ElasticsearchConnectionOptions();
        configuration.Configure(options);

        Assert.True(options.ConfigurationExists());
        Assert.Equal("https://es.example", options.Url);
        Assert.Equal([9243], options.Ports);
        Assert.Equal(ElasticsearchAuthenticationType.Basic, options.AuthenticationType);
        Assert.Equal("elastic", options.Username);
        Assert.Equal("pw", options.Password);
        Assert.Equal("AA:BB", options.CertificateFingerprint);
    }

    [Fact]
    public void ElasticsearchReadsAnApiKeyOrAKeyIdAndKey()
    {
        var connection = new ConnectionDefinition { Key = "es", DisplayName = "es", Protocol = ConnectorProtocols.Sdk, BaseUrl = "http://localhost:9200" };

        var apiKey = ElasticsearchConnectionOptionsConfigurations.Apply(connection, new ConnectionCredential { Values = new Dictionary<string, string> { [CredentialKeys.ApiKey] = "k" } });
        Assert.Equal(ElasticsearchAuthenticationType.ApiKey, apiKey.AuthenticationType);
        Assert.Equal("k", apiKey.ApiKey);

        var keyPair = ElasticsearchConnectionOptionsConfigurations.Apply(connection, new ConnectionCredential { Values = new Dictionary<string, string> { ["keyId"] = "id", ["key"] = "secret" } });
        Assert.Equal(ElasticsearchAuthenticationType.KeyIdAndKey, keyPair.AuthenticationType);
        Assert.Equal("id", keyPair.KeyId);
        Assert.Equal("secret", keyPair.Key);
    }

    private static MetaConversionsApiService Conversions(FakeConnectorEngine engine, FacebookPixelSettings settings) => new(
        engine,
        Sites.With(new JsonObject { [nameof(FacebookPixelSettings)] = JsonSerializerNode(settings) }),
        new FakeCallerAccessor(),
        new FakeCallerFactory(),
        NullLogger<MetaConversionsApiService>.Instance,
        new PlainLocalizer<MetaConversionsApiService>());

    private static JsonNode JsonSerializerNode<T>(T value) => System.Text.Json.JsonSerializer.SerializeToNode(value)!;

    private static ShellConfiguration Shell(Dictionary<string, string?> values) =>
        new ShellConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    private static ServiceProvider Engine(FakeConnectorEngine engine) => new ServiceCollection()
        .AddSingleton<IConnectorEngine>(engine)
        .AddSingleton<ICallerContextFactory>(new FakeCallerFactory())
        .BuildServiceProvider();
}
