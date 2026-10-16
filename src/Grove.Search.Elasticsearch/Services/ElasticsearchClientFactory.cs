using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging;
using Grove.Environment.Shell.Configuration;
using Grove.Search.Elasticsearch.Models;

namespace Grove.Search.Elasticsearch.Services;

/// <summary>
/// Builds the Elasticsearch client from the options its connection gives. The client sends
/// through <see cref="EngineRoutedHttpHandler"/> (the connector engine's guarded pipeline, with
/// the connection's rate limit, retries and response cap); the transport's own retries, sniffing
/// and pinging are off so attempts never multiply and every request is one engine call.
/// </summary>
public sealed class ElasticsearchClientFactory : IElasticsearchClientFactory
{
    private readonly IServiceProvider _services;
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    public ElasticsearchClientFactory(IServiceProvider services, IShellConfiguration shellConfiguration, ILogger<ElasticsearchClientFactory> logger)
    {
        _services = services;
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    public ElasticsearchClient Create(ElasticsearchConnectionOptions configuration)
    {
        AuthorizationHeader authentication = configuration.AuthenticationType switch
        {
            ElasticsearchAuthenticationType.ApiKey => new ApiKey(configuration.ApiKey),
            ElasticsearchAuthenticationType.Base64ApiKey => new Base64ApiKey(configuration.Base64ApiKey),
            ElasticsearchAuthenticationType.KeyIdAndKey => new Base64ApiKey(Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configuration.KeyId}:{configuration.Key}"))),
            _ => new BasicAuthentication(configuration.Username, configuration.Password),
        };

        var nodes = GetNodeUris(configuration).ToArray();
        if (nodes.Length == 0 && configuration.ConnectionType != ElasticsearchConnectionType.CloudConnectionPool)
        {
            // No connection defined yet: the feature stays inert. The placeholder node is never
            // reached, because every request goes through the engine, which refuses an unknown
            // connection.
            _logger.LogWarning("Elasticsearch has no connection: define the '{Connection}' connection to use it.", ElasticsearchConnectionOptionsConfigurations.ConnectionKey(_shellConfiguration));
            nodes = [new Uri("http://localhost:9200")];
        }

        NodePool pool = configuration.ConnectionType switch
        {
            ElasticsearchConnectionType.CloudConnectionPool => new CloudNodePool(configuration.CloudId, authentication),
            ElasticsearchConnectionType.StaticConnectionPool => new StaticNodePool(nodes),
            // Sniffing would discover nodes past the connection's base URL: a static pool instead.
            ElasticsearchConnectionType.SniffingConnectionPool => new StaticNodePool(nodes),
            ElasticsearchConnectionType.StickyConnectionPool => new StickyNodePool(nodes),
            _ => new SingleNodePool(nodes[0]),
        };

        var connectionKey = ElasticsearchConnectionOptionsConfigurations.ConnectionKey(_shellConfiguration);
        var invoker = new HttpRequestInvoker((_, _) => new EngineRoutedHttpHandler(_services, connectionKey));

        var settings = new ElasticsearchClientSettings(pool, invoker)
            .Authentication(authentication)
            .MaximumRetries(0)
            .DisablePing()
            .SniffOnStartup(false)
            .SniffOnConnectionFault(false);

        // The connection's certificate fingerprint (es:certificateFingerprint) is pinned by the
        // engine's guarded client, which carries every request.

        if (configuration.EnableDebugMode)
        {
            settings.EnableDebugMode(details =>
            {
                if (!_logger.IsEnabled(LogLevel.Debug))
                {
                    return;
                }

                if (details.RequestBodyInBytes != null)
                {
                    _logger.LogDebug("Elasticsearch request is: {Request}.", Encoding.UTF8.GetString(details.RequestBodyInBytes));
                }

                if (details.ResponseBodyInBytes != null)
                {
                    _logger.LogDebug("Elasticsearch response is: {Response}.", Encoding.UTF8.GetString(details.ResponseBodyInBytes));
                }
            });
        }

        if (configuration.EnableHttpCompression)
        {
            settings.EnableHttpCompression();
        }

        return new ElasticsearchClient(settings);
    }

    private static IEnumerable<Uri> GetNodeUris(ElasticsearchConnectionOptions elasticConfiguration)
    {
        if (string.IsNullOrEmpty(elasticConfiguration.Url))
        {
            return [];
        }

        return elasticConfiguration.Ports.Select(port => new Uri($"{elasticConfiguration.Url}:{port}")).Distinct();
    }
}
