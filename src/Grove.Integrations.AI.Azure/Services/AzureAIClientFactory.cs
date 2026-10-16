using System.Collections.Concurrent;
using Azure.Core.Pipeline;
using Azure.Identity;
using Grove.Infrastructure.Settings;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Options;
using Grove.Integrations.AI.Azure.Models;

namespace Grove.Integrations.AI.Azure.Services;

/// <summary>
/// Builds the Azure AI Search clients from the options its connection gives. Every client sends
/// through <see cref="EngineRoutedHttpHandler"/> (the connector engine's guarded pipeline, with
/// the connection's rate limit, retries and response cap) with the SDK's own retries off.
/// </summary>
public sealed class AzureAIClientFactory : IDisposable
{
    private readonly IOptionsMonitor<AzureAISearchDefaultOptions> _defaultOptions;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;
    private readonly IDisposable _optionsChangeRegistration;
    private readonly object _syncLock = new();

    private SearchIndexClient _searchIndexClient;

    private ConcurrentDictionary<string, SearchClient> _clients;

    private HttpClient _transportClient;

    public AzureAIClientFactory(IOptionsMonitor<AzureAISearchDefaultOptions> defaultOptions, ISiteService siteService, IServiceProvider services)
    {
        _defaultOptions = defaultOptions;
        _siteService = siteService;
        _services = services;
        _optionsChangeRegistration = _defaultOptions.OnChange((_, _) => ResetClients());
    }

    /// <summary>The SDK options every client is built with: the engine's pipeline as transport, no SDK retry.</summary>
    public SearchClientOptions CreateClientOptions()
    {
        lock (_syncLock)
        {
            _transportClient ??= new HttpClient(
                new EngineRoutedHttpHandler(_services, AzureAISearchDefaultOptionsConfigurations.ConnectionKey(_siteService.GetSettings<AzureAISearchDefaultSettings>())),
                disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
        }

        var options = new SearchClientOptions
        {
            Transport = new HttpClientTransport(_transportClient),
        };
        options.Retry.MaxRetries = 0;

        return options;
    }

    public SearchClient CreateSearchClient(string indexFullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexFullName, nameof(indexFullName));

        _clients ??= [];
        var defaultOptions = _defaultOptions.CurrentValue;

        if (!_clients.TryGetValue(indexFullName, out var client))
        {
            if (!defaultOptions.ConfigurationExists())
            {
                throw new Exception("Azure AI was not configured.");
            }

            if (!Uri.TryCreate(defaultOptions.Endpoint, UriKind.Absolute, out var endpoint))
            {
                throw new Exception("The Endpoint provided to Azure AI Options contains invalid value.");
            }

            if (defaultOptions.AuthenticationType == AzureAIAuthenticationType.ApiKey && defaultOptions.Credential != null)
            {
                client = new SearchClient(endpoint, indexFullName, defaultOptions.Credential, CreateClientOptions());
            }
            else if (defaultOptions.AuthenticationType == AzureAIAuthenticationType.ManagedIdentity)
            {
                client = new SearchClient(endpoint, indexFullName, GetManagedIdentityCredential(defaultOptions), CreateClientOptions());
            }
            else
            {
                client = new SearchClient(endpoint, indexFullName, new DefaultAzureCredential(), CreateClientOptions());
            }

            _clients.TryAdd(indexFullName, client);
        }

        return client;
    }

    public SearchIndexClient CreateSearchIndexClient()
    {
        var defaultOptions = _defaultOptions.CurrentValue;

        if (_searchIndexClient == null)
        {
            if (!defaultOptions.ConfigurationExists())
            {
                throw new Exception("Azure AI was not configured.");
            }

            if (!Uri.TryCreate(defaultOptions.Endpoint, UriKind.Absolute, out var endpoint))
            {
                throw new Exception("The Endpoint provided to Azure AI Options contains invalid value.");
            }

            if (defaultOptions.AuthenticationType == AzureAIAuthenticationType.ApiKey && defaultOptions.Credential != null)
            {
                _searchIndexClient = new SearchIndexClient(endpoint, defaultOptions.Credential, CreateClientOptions());
            }
            else if (defaultOptions.AuthenticationType == AzureAIAuthenticationType.ManagedIdentity)
            {
                _searchIndexClient = new SearchIndexClient(endpoint, GetManagedIdentityCredential(defaultOptions), CreateClientOptions());
            }
            else
            {
                _searchIndexClient = new SearchIndexClient(endpoint, new DefaultAzureCredential(), CreateClientOptions());
            }
        }

        return _searchIndexClient;
    }

    public void Dispose()
    {
        _optionsChangeRegistration.Dispose();
        _transportClient?.Dispose();
    }

    private static ManagedIdentityCredential GetManagedIdentityCredential(AzureAISearchDefaultOptions defaultOptions)
        => !string.IsNullOrEmpty(defaultOptions.IdentityClientId)
        ? new(ManagedIdentityId.FromUserAssignedClientId(defaultOptions.IdentityClientId))
        : new(ManagedIdentityId.SystemAssigned);

    private void ResetClients()
    {
        lock (_syncLock)
        {
            _clients = null;
            _searchIndexClient = null;

            // The connection may have been renamed: the next client takes a transport on the current one.
            _transportClient = null;
        }
    }
}
