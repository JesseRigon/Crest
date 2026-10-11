using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Login.ComponentProviders;
using Crest.Workflows.Studio.Login.Contracts;
using Crest.Workflows.Studio.Login.HttpMessageHandlers;
using Crest.Workflows.Studio.Login.Models;
using Crest.Workflows.Studio.Login.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Studio.Login.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IServiceCollection"/> interface.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the login module to the service collection.
    /// </summary>
    public static IServiceCollection AddLoginModuleCore(this IServiceCollection services)
    {
        return services
                .AddScoped<IFeature, LoginFeature>()
                .AddOptions()
                .AddAuthorizationCore()
                .AddScoped<AuthenticatingApiHttpMessageHandler>()
                .AddScoped<AuthenticationStateProvider, AccessTokenAuthenticationStateProvider>()
                .AddScoped<IUnauthorizedComponentProvider, RedirectToLoginUnauthorizedComponentProvider>()
            ;
    }

    /// <summary>
    /// Configures the login module to use crestWorkflows identity
    /// </summary>
    public static IServiceCollection UseCrestWorkflowsIdentity(this IServiceCollection services)
    {
        return services
                .AddScoped<ICredentialsValidator, IdentityCredentialsValidator>()
                .AddScoped<IAuthorizationService, IdentityAuthorizationService>()
                .AddScoped<IRefreshTokenService, IdentityRefreshTokenService>()
                .AddScoped<IEndSessionService, IdentityEndSessionService>()
                .AddScoped<IAuthenticationProviderManager, DefaultAuthenticationProviderManager>()
                .AddScoped<IAuthenticationProvider, JwtAuthenticationProvider>();
            ;
    }

    /// <summary>
    /// Configures the service collection to use OAuth2 for credential validation and related services.
    /// </summary>
    public static IServiceCollection UseOAuth2(this IServiceCollection services, Action<OAuth2CredentialsValidatorOptions> configure)
    {
        services.Configure(configure);
        
        services.AddHttpClient<OAuth2HttpClient>(httpClient =>
        {
            var options = services.BuildServiceProvider().GetRequiredService<IOptions<OAuth2CredentialsValidatorOptions>>().Value;
            httpClient.BaseAddress = new(options.TokenEndpoint);
        });
        
        return services
                .AddScoped<ICredentialsValidator, OAuth2CredentialsValidator>()
                .AddScoped<IAuthorizationService, IdentityAuthorizationService>()
                .AddScoped<IRefreshTokenService, IdentityRefreshTokenService>()
                .AddScoped<IAuthenticationProviderManager, DefaultAuthenticationProviderManager>()
                .AddScoped<IEndSessionService, IdentityEndSessionService>()
                .AddScoped<IAuthenticationProviderManager, DefaultAuthenticationProviderManager>()
            ;
    }

    /// <summary>
    /// Configures the login module to use OpenIdConnect (OIDC)
    /// </summary>
    public static IServiceCollection UseOpenIdConnect(this IServiceCollection services, Action<OpenIdConnectConfiguration> configure)
    {
        services.Configure(configure);

        return services
                .AddScoped<IAuthorizationService, OpenIdConnectAuthorizationService>()
                .AddScoped<IRefreshTokenService, OpenIdConnectRefreshTokenService>()
                .AddScoped<IEndSessionService, OpenIdConnectEndSessionService>()
            ;
    }
}