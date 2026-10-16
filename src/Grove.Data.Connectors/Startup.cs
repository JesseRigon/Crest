using Grove.Access;
using Grove.Data.Connectors.Api;
using Grove.Data.Connectors.Models;
using Grove.Data.Connectors.Services;
using Grove.Data.Protocols.GraphQL.Client;
using Grove.Data.Protocols.Http;
using Grove.Data.Protocols.OAuth;
using Grove.Data.Protocols.Rest;
using Grove.Data.Protocols.Soap;
using Grove.Data.Protocols.Sql;
using Grove.Data.Protocols.Webhooks;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Security.Permissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grove.Data.Connectors;

/// <summary>
/// The connector engine (docs/connectors.md): the shipped protocols, the registry's two layers,
/// the sealed credential store and its scopes, the OAuth lifecycle, the execution pipeline, the
/// webhook verifier, the connections' application callers, the API and the two pages
/// (Grove.Data.Connectors.BlazorWasm). Options come from the shell section <c>Connectors</c>.
/// </summary>
public sealed class Startup(IShellConfiguration shellConfiguration) : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ConnectorOptions>(shellConfiguration.GetSection(ConnectorOptions.ConfigurationSection));
        services.AddMemoryCache();

        services
            .AddConnectorProtocol<HttpConnectorProtocol>()
            .AddConnectorProtocol<RestConnectorProtocol>()
            .AddConnectorProtocol<SoapConnectorProtocol>()
            .AddConnectorProtocol<GraphQLConnectorProtocol>()
            .AddConnectorProtocol<SqlConnectorProtocol>()
            .AddConnectorProtocol<WebhookConnectorProtocol>()
            .AddWebhookSignatureScheme<HmacSha256HexScheme>()
            .AddWebhookSignatureScheme<HmacSha256Base64Scheme>()
            .AddCredentialScope<TenantScopeHandler>()
            .AddCredentialScope<UserScopeHandler>();

        // Per shell: the guarded client, the limiters and the client-credentials tokens.
        services.AddSingleton<ConnectorHttp>();
        services.AddSingleton<ConnectorLimits>();
        services.AddSingleton<ConnectorTokenCache>();
        services.TryAddSingleton<IOAuthClient, OAuthClient>();

        services.AddScoped<IConnectionSource, DeclaredConnectionSource>();
        services.AddScoped<IConnectionSource, DefinedConnectionSource>();
        services.AddScoped<DefinedConnectionStore>();
        services.AddScoped<IDefinedConnectionStore>(sp => sp.GetRequiredService<DefinedConnectionStore>());
        services.AddScoped<SchemaLoader>();
        services.AddScoped<ConnectionRegistry>();
        services.AddScoped<IConnectionRegistry>(sp => sp.GetRequiredService<ConnectionRegistry>());

        services.AddScoped<CredentialStore>();
        services.AddScoped<ICredentialStore>(sp => sp.GetRequiredService<CredentialStore>());
        services.AddScoped<IAppCredentialsStore>(sp => sp.GetRequiredService<CredentialStore>());
        services.AddScoped<ICredentialResolver, CredentialResolver>();
        services.AddScoped<AppCredentialsProvider>();
        services.AddScoped<IAppCredentialsProvider>(sp => sp.GetRequiredService<AppCredentialsProvider>());

        services.AddScoped<ConnectorTokenService>();
        services.AddScoped<IOAuthStateProtector, OAuthStateProtector>();
        services.AddScoped<IConnectionAuthorizationService, ConnectionAuthorizationService>();
        services.AddScoped<IConnectorEngine, ConnectorEngine>();
        services.AddScoped<IWebhookVerifier, WebhookVerifier>();
        services.AddScoped<ICallerContextContributor, ConnectionCallerContributor>();

        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<ConnectionsApi>();
    }
}
