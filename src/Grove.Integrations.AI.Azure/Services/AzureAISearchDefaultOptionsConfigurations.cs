#nullable enable

using Azure;
using Grove.Access;
using Grove.Data.Connectors;
using Grove.Environment.Shell.Configuration;
using Grove.Infrastructure.Settings;
using Grove.Integrations.AI.Azure.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.AI.Azure.Services;

/// <summary>
/// Azure AI Search's options: the endpoint and credentials from the connection the settings name
/// (opened through the connector engine's SDK seam, as the tenant's system actor), the index
/// prefix and analyzers from the host's <c>Grove_AzureAISearch</c> section.
/// </summary>
public sealed class AzureAISearchDefaultOptionsConfigurations : IConfigureOptions<AzureAISearchDefaultOptions>
{
    /// <summary>The connection setting naming a user-assigned managed identity.</summary>
    public const string IdentityClientIdSetting = "azure:identityClientId";

    /// <summary>The connection setting choosing <c>ManagedIdentity</c> or <c>Default</c> when the connection has no API key.</summary>
    public const string AuthenticationSetting = "azure:authentication";

    private readonly IShellConfiguration _shellConfiguration;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public AzureAISearchDefaultOptionsConfigurations(
        IShellConfiguration shellConfiguration,
        ISiteService siteService,
        IServiceProvider services,
        ILogger<AzureAISearchDefaultOptionsConfigurations> logger)
    {
        _shellConfiguration = shellConfiguration;
        _siteService = siteService;
        _services = services;
        _logger = logger;
    }

    public void Configure(AzureAISearchDefaultOptions options)
    {
        var fileOptions = _shellConfiguration.GetSection("Grove_AzureAISearch").Get<AzureAISearchDefaultOptions>() ?? new AzureAISearchDefaultOptions();

        options.IndexesPrefix = fileOptions.IndexesPrefix;
        options.Analyzers = fileOptions.Analyzers is { Length: > 0 } ? fileOptions.Analyzers : AzureAISearchDefaultOptions.DefaultAnalyzers;

        var key = ConnectionKey(_siteService.GetSettings<AzureAISearchDefaultSettings>());
        var connection = SdkConnections.Open(_services, key, Apply, _logger);

        if (connection is not null)
        {
            options.Endpoint = connection.Endpoint;
            options.AuthenticationType = connection.AuthenticationType;
            options.Credential = connection.Credential;
            options.IdentityClientId = connection.IdentityClientId;
        }

        options.SetConfigurationExists(HasConnectionInfo(options));
    }

    public static string ConnectionKey(AzureAISearchDefaultSettings? settings) =>
        string.IsNullOrWhiteSpace(settings?.Connection) ? AzureAISearchDefaultSettings.DefaultConnection : settings.Connection.Trim();

    /// <summary>Maps the connection and its credential onto the client's options.</summary>
    public static AzureAISearchDefaultOptions Apply(ConnectionDefinition connection, ConnectionCredential? credential)
    {
        var options = new AzureAISearchDefaultOptions
        {
            Endpoint = connection.BaseUrl,
            IdentityClientId = connection.Setting(IdentityClientIdSetting),
        };

        if (credential?[CredentialKeys.ApiKey] is { } apiKey)
        {
            options.AuthenticationType = AzureAIAuthenticationType.ApiKey;
            options.Credential = new AzureKeyCredential(apiKey);
        }
        else
        {
            options.AuthenticationType = options.IdentityClientId is not null
                || string.Equals(connection.Setting(AuthenticationSetting), nameof(AzureAIAuthenticationType.ManagedIdentity), StringComparison.OrdinalIgnoreCase)
                ? AzureAIAuthenticationType.ManagedIdentity
                : AzureAIAuthenticationType.Default;
        }

        return options;
    }

    private static bool HasConnectionInfo(AzureAISearchDefaultOptions options)
    {
        if (string.IsNullOrEmpty(options.Endpoint))
        {
            return false;
        }

        return options.AuthenticationType != AzureAIAuthenticationType.ApiKey ||
            !string.IsNullOrEmpty(options.Credential?.Key);
    }
}

/// <summary>Opens an SDK connection's definition and credential through the engine, for a client's construction.</summary>
internal static class SdkConnections
{
    public static T? Open<T>(IServiceProvider services, string key, Func<ConnectionDefinition, ConnectionCredential?, T> map, ILogger logger)
        where T : class
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
                (context, _) => Task.FromResult(map(context.Connection, context.Credential)),
                new SdkCallOptions { IsSafeToRetry = true, MaxRetries = 0, Scope = CredentialScope.Tenant })
                .GetAwaiter().GetResult();

            if (!result.Succeeded)
            {
                logger.LogWarning("The connection '{Key}' could not be opened ({Outcome}): {Error}", key, result.Outcome, result.Error);
            }

            return result.Succeeded ? result.Result : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The connection '{Key}' could not be opened.", key);

            return null;
        }
    }
}

/// <summary>Refreshes the options when the connection they run on changes.</summary>
internal sealed class AzureAISearchConnectionChangeHandler(ISiteService siteService, Grove.Environment.Options.IOptionsUpdateNotifier notifier) : IConnectionChangeHandler
{
    public async Task ConnectionChangedAsync(string key, ConnectionDefinition? definition, CancellationToken cancellationToken = default)
    {
        var settings = await siteService.GetSettingsAsync<AzureAISearchDefaultSettings>();
        if (string.Equals(key, AzureAISearchDefaultOptionsConfigurations.ConnectionKey(settings), StringComparison.OrdinalIgnoreCase))
        {
            notifier.RequestUpdate<AzureAISearchDefaultOptions>();
        }
    }
}
