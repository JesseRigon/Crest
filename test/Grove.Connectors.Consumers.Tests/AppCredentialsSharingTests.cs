using System.Text.Json.Nodes;
using Grove.Data.Connectors;
using Grove.Infrastructure.Settings;
using Grove.Integrations.GitHub;
using Grove.Integrations.Google;
using Grove.Integrations.Google.Authentication.Configuration;
using Grove.Integrations.Google.Authentication.Settings;
using Grove.Integrations.Google.Connections;
using Grove.Integrations.Microsoft;
using Grove.Integrations.Microsoft.Configuration;
using Grove.Integrations.Microsoft.Connections;
using Grove.Integrations.Microsoft.Settings;
using Grove.Integrations.Twitter;
using Grove.Integrations.Twitter.Connections;
using Grove.Integrations.Twitter.Signin.Configuration;
using Grove.Integrations.Twitter.Signin.Services;
using Grove.Integrations.Twitter.Signin.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Grove.Connectors.Consumers.Tests;

/// <summary>A vendor's app is set up once: its sign-in provider reads the app credentials its connection declares.</summary>
public sealed class AppCredentialsSharingTests
{
    [Fact]
    public void GoogleSignInRunsOnTheAppCredentialsTheGoogleConnectionDeclares()
    {
        var connection = Declared(services => services.AddGoogleConnection());
        Assert.Equal(AuthSchemes.OAuth2AuthorizationCode, connection.Auth.Scheme);
        Assert.Equal([CredentialScope.Tenant, CredentialScope.User], connection.AllowedScopes);

        var apps = new FakeAppCredentials(new() { [connection.Auth.AppCredentialsKey!] = new AppCredentials("google-client", "google-secret") });
        var configuration = new GoogleOptionsConfiguration(
            Options.Create(new GoogleAuthenticationSettings { CallbackPath = "/signin-g", SaveTokens = true }),
            Provider(apps),
            NullLogger<GoogleOptionsConfiguration>.Instance);

        var authentication = new AuthenticationOptions();
        configuration.Configure(authentication);
        var google = new GoogleOptions();
        configuration.Configure(GoogleDefaults.AuthenticationScheme, google);

        Assert.Contains(authentication.Schemes, scheme => scheme.Name == GoogleDefaults.AuthenticationScheme);
        Assert.Equal("google-client", google.ClientId);
        Assert.Equal("google-secret", google.ClientSecret);
        Assert.Equal("/signin-g", google.CallbackPath);
        Assert.True(google.SaveTokens);
        Assert.All(apps.Reads, key => Assert.Equal(GoogleConstants.AppCredentialsKey, key));
    }

    [Fact]
    public void SignInIsNotRegisteredWithoutAppCredentials()
    {
        var configuration = new GoogleOptionsConfiguration(
            Options.Create(new GoogleAuthenticationSettings()),
            Provider(new FakeAppCredentials([])),
            NullLogger<GoogleOptionsConfiguration>.Instance);

        var authentication = new AuthenticationOptions();
        configuration.Configure(authentication);

        Assert.Empty(authentication.Schemes);
    }

    [Fact]
    public void SignInWithXRunsOnTheXAppCredentialsOverOAuth2()
    {
        var connection = Declared(services => services.AddXConnection());
        Assert.Equal(TwitterConstants.AppCredentialsKey, connection.Auth.AppCredentialsKey);
        Assert.Equal(ConnectorProtocols.Rest, connection.Protocol);
        Assert.True(connection.Auth.UsePkce);
        Assert.Contains("tweet.write", connection.Auth.Scopes);
        Assert.Contains(connection.Operations, operation => operation.Id == TwitterConstants.Operations.CreatePost && operation.Method == "POST" && !operation.IsSafeToRetry);

        var signin = Substitute.For<ITwitterSigninService>();
        signin.GetSettingsAsync().Returns(new TwitterSigninSettings { CallbackPath = "/signin-x" });
        var apps = new FakeAppCredentials(new() { [TwitterConstants.AppCredentialsKey] = new AppCredentials("x-client", "x-secret") });
        var configuration = new TwitterOptionsConfiguration(Provider(apps), signin, new HttpContextAccessor(), NullLogger<TwitterOptionsConfiguration>.Instance);

        var authentication = new AuthenticationOptions();
        configuration.Configure(authentication);
        var x = new XAuthenticationOptions();
        configuration.Configure(TwitterConstants.AuthenticationScheme, x);

        var scheme = Assert.Single(authentication.Schemes);
        Assert.Equal(TwitterConstants.AuthenticationScheme, scheme.Name);
        Assert.Equal(typeof(XAuthenticationHandler), scheme.HandlerType);
        Assert.Equal("x-client", x.ClientId);
        Assert.Equal("x-secret", x.ClientSecret);
        Assert.True(x.UsePkce);
    }

    [Fact]
    public async Task MicrosoftSignInAndGraphShareOneAppOnTheEntraTenantsAuthority()
    {
        var siteService = Sites.With(new JsonObject { [nameof(AzureADSettings)] = new JsonObject { ["TenantId"] = "contoso.onmicrosoft.com" } });
        var connection = Assert.Single(await new MicrosoftGraphConnectionSource(siteService).GetConnectionsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(MicrosoftAuthenticationConstants.GraphConnectionKey, connection.Key);
        Assert.Equal(ConnectionOrigin.Declared, connection.Origin);
        Assert.StartsWith("https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/", connection.Auth.AuthorizeUrl);
        Assert.EndsWith(":contoso.onmicrosoft.com", connection.Version);
        Assert.Equal(MicrosoftAuthenticationConstants.AppCredentialsKey, connection.Auth.AppCredentialsKey);

        var apps = new FakeAppCredentials(new() { [MicrosoftAuthenticationConstants.AppCredentialsKey] = new AppCredentials("ms-client", "ms-secret") });
        var configuration = new MicrosoftAccountOptionsConfiguration(Options.Create(new MicrosoftAccountSettings()), Provider(apps), NullLogger<MicrosoftAccountOptionsConfiguration>.Instance);
        var options = new MicrosoftAccountOptions();
        configuration.Configure(MicrosoftAccountDefaults.AuthenticationScheme, options);

        Assert.Equal("ms-client", options.ClientId);
        Assert.Equal("ms-secret", options.ClientSecret);
    }

    [Fact]
    public async Task GraphFallsBackToTheCommonAuthorityWithoutAnEntraTenant()
    {
        var connection = Assert.Single(await new MicrosoftGraphConnectionSource(Sites.With([])).GetConnectionsAsync(TestContext.Current.CancellationToken));

        Assert.Contains("/common/", connection.Auth.AuthorizeUrl);
    }

    [Fact]
    public async Task ASettingsSecretMovesIntoTheAppCredentialsAndLeavesTheSettings()
    {
        var dataProtection = new EphemeralDataProtectionProvider();
        var sealedSecret = dataProtection.CreateProtector(GitHubConstants.Features.GitHubAuthentication).Protect("gh-secret");
        var settings = new JsonObject { ["ClientID"] = "gh-client", ["ClientSecret"] = sealedSecret, ["CallbackPath"] = "/signin-gh" };
        var siteService = Sites.With(new JsonObject { ["GitHubAuthenticationSettings"] = settings });
        var store = new FakeCredentialStore();
        var services = new ServiceCollection()
            .AddSingleton(siteService)
            .AddSingleton<IDataProtectionProvider>(dataProtection)
            .AddSingleton<ICredentialStore>(store)
            .BuildServiceProvider();

        await Grove.Integrations.GitHub.Services.AppCredentialsAccess.MoveFromSiteSettingsAsync(
            services, "GitHubAuthenticationSettings", "ClientID", "ClientSecret", GitHubConstants.Features.GitHubAuthentication, GitHubConstants.AppCredentialsKey, NullLogger.Instance);

        var credential = store.Saved[(GitHubConstants.ConnectionKey, CredentialOwner.Tenant)];
        Assert.Equal("gh-client", credential[CredentialKeys.ClientId]);
        Assert.Equal("gh-secret", credential[CredentialKeys.ClientSecret]);
        Assert.False(settings.ContainsKey("ClientID"));
        Assert.False(settings.ContainsKey("ClientSecret"));
        Assert.Equal("/signin-gh", settings["CallbackPath"]!.GetValue<string>());
        await siteService.Received(1).UpdateSiteSettingsAsync(Arg.Any<ISite>());
    }

    private static ConnectionDefinition Declared(Action<IServiceCollection> declare)
    {
        var services = new ServiceCollection();
        declare(services);

        return Assert.Single(services.BuildServiceProvider().GetServices<ConnectionDeclaration>()).Definition;
    }

    private static ServiceProvider Provider(IAppCredentialsProvider apps) =>
        new ServiceCollection().AddSingleton(apps).BuildServiceProvider();
}
