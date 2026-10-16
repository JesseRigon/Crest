#nullable enable

using Grove.Access;
using Grove.Data.Connectors;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Search.Elasticsearch.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Search.Elasticsearch.Services;

/// <summary>
/// Elasticsearch's connection options, from the SDK connection the host's
/// <c>Grove_Elasticsearch:Connection</c> names (<see cref="DefaultConnection"/> by default),
/// opened through the connector engine as the tenant's system actor. The connection's base URL
/// is the node (scheme, host and port); its settings carry the pool, the cloud id and the
/// certificate fingerprint; its credential the API key, the user name and password, or the key
/// id and key. Index prefix, analyzers and token filters stay the host's.
/// </summary>
public sealed class ElasticsearchConnectionOptionsConfigurations : IConfigureOptions<ElasticsearchConnectionOptions>
{
    public const string ConfigSectionName = "Grove_Elasticsearch";

    public const string DefaultConnection = "elasticsearch";

    public const string ConnectionTypeSetting = "es:connectionType";
    public const string PortsSetting = "es:ports";
    public const string CloudIdSetting = "es:cloudId";
    public const string CertificateFingerprintSetting = "es:certificateFingerprint";
    public const string EnableDebugModeSetting = "es:enableDebugMode";
    public const string EnableHttpCompressionSetting = "es:enableHttpCompression";

    /// <summary>The credential values beside <see cref="CredentialKeys"/>.</summary>
    public const string Base64ApiKeyValue = "base64ApiKey";
    public const string KeyIdValue = "keyId";
    public const string KeyValue = "key";

    private readonly IShellConfiguration _shellConfiguration;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public ElasticsearchConnectionOptionsConfigurations(IShellConfiguration shellConfiguration, IServiceProvider services, ILogger<ElasticsearchConnectionOptionsConfigurations> logger)
    {
        _shellConfiguration = shellConfiguration;
        _services = services;
        _logger = logger;
    }

    public static string ConnectionKey(IShellConfiguration configuration) =>
        configuration.GetSection(ConfigSectionName)["Connection"] is { Length: > 0 } key ? key.Trim() : DefaultConnection;

    public void Configure(ElasticsearchConnectionOptions options)
    {
        var opened = Open(_services, ConnectionKey(_shellConfiguration), _logger);
        if (opened is not null)
        {
            Copy(opened, options);
        }

        if (options.Ports == null || options.Ports.Length == 0)
        {
            options.Ports = [9200];
        }
    }

    /// <summary>Maps a connection and its credential onto the client's options.</summary>
    public static ElasticsearchConnectionOptions Apply(ConnectionDefinition connection, ConnectionCredential? credential)
    {
        var options = new ElasticsearchConnectionOptions
        {
            ConnectionType = Enum.TryParse<ElasticsearchConnectionType>(connection.Setting(ConnectionTypeSetting), ignoreCase: true, out var type) ? type : ElasticsearchConnectionType.SingleNodeConnectionPool,
            CloudId = connection.Setting(CloudIdSetting),
            CertificateFingerprint = connection.Setting(CertificateFingerprintSetting),
            EnableDebugMode = bool.TryParse(connection.Setting(EnableDebugModeSetting), out var debug) && debug,
            EnableHttpCompression = !bool.TryParse(connection.Setting(EnableHttpCompressionSetting), out var compression) || compression,
        };

        if (Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var node))
        {
            options.Url = $"{node.Scheme}://{node.Host}";
            options.Ports = connection.Setting(PortsSetting) is { } ports
                ? [.. ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse)]
                : [node.Port];
        }

        if (credential?[CredentialKeys.ApiKey] is { } apiKey)
        {
            options.AuthenticationType = ElasticsearchAuthenticationType.ApiKey;
            options.ApiKey = apiKey;
        }
        else if (credential?[Base64ApiKeyValue] is { } base64ApiKey)
        {
            options.AuthenticationType = ElasticsearchAuthenticationType.Base64ApiKey;
            options.Base64ApiKey = base64ApiKey;
        }
        else if (credential?[KeyIdValue] is { } keyId)
        {
            options.AuthenticationType = ElasticsearchAuthenticationType.KeyIdAndKey;
            options.KeyId = keyId;
            options.Key = credential[KeyValue];
        }
        else
        {
            options.AuthenticationType = ElasticsearchAuthenticationType.Basic;
            options.Username = credential?[CredentialKeys.Username];
            options.Password = credential?[CredentialKeys.Password];
        }

        return options;
    }

    private static ElasticsearchConnectionOptions? Open(IServiceProvider services, string key, ILogger logger)
    {
        try
        {
            using var scope = services.CreateScope();
            var engine = scope.ServiceProvider.GetService<IConnectorEngine>();
            var callers = scope.ServiceProvider.GetService<ICallerContextFactory>();
            if (engine is null || callers is null)
            {
                return null;
            }

            var caller = callers.CreateSystemAsync().GetAwaiter().GetResult();
            var result = engine.RunSdkAsync(
                key,
                caller,
                (context, _) => Task.FromResult(Apply(context.Connection, context.Credential)),
                new SdkCallOptions { IsSafeToRetry = true, MaxRetries = 0, Scope = CredentialScope.Tenant })
                .GetAwaiter().GetResult();

            if (!result.Succeeded)
            {
                logger.LogWarning("The Elasticsearch connection '{Key}' could not be opened ({Outcome}): {Error}", key, result.Outcome, result.Error);
            }

            return result.Succeeded ? result.Result : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The Elasticsearch connection '{Key}' could not be opened.", key);

            return null;
        }
    }

    private static void Copy(ElasticsearchConnectionOptions from, ElasticsearchConnectionOptions to)
    {
        to.Url = from.Url;
        to.Ports = from.Ports;
        to.ConnectionType = from.ConnectionType;
        to.AuthenticationType = from.AuthenticationType;
        to.ApiKey = from.ApiKey;
        to.Base64ApiKey = from.Base64ApiKey;
        to.CloudId = from.CloudId;
        to.Username = from.Username;
        to.Password = from.Password;
        to.KeyId = from.KeyId;
        to.Key = from.Key;
        to.EnableHttpCompression = from.EnableHttpCompression;
        to.EnableDebugMode = from.EnableDebugMode;
        to.CertificateFingerprint = from.CertificateFingerprint;
    }
}

/// <summary>The client is built once per tenant: a change to its connection reloads the tenant.</summary>
internal sealed class ElasticsearchConnectionChangeHandler(IShellConfiguration configuration, IShellReleaseManager shellReleaseManager) : IConnectionChangeHandler
{
    public Task ConnectionChangedAsync(string key, ConnectionDefinition? definition, CancellationToken cancellationToken = default)
    {
        if (string.Equals(key, ElasticsearchConnectionOptionsConfigurations.ConnectionKey(configuration), StringComparison.OrdinalIgnoreCase))
        {
            shellReleaseManager.RequestRelease();
        }

        return Task.CompletedTask;
    }
}
