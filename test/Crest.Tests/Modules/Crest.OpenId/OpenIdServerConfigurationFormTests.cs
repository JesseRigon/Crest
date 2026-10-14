using Crest.OpenId.Controllers;
using Crest.OpenId.Settings;

namespace Crest.Tests.Modules.Crest.OpenId;

// The OpenID server configuration as a structured form (step 7: the MVC editor and its display
// driver are retired): what the form shows is what it writes back.
public class OpenIdServerConfigurationFormTests
{
    [Fact]
    public void TheFormRoundTripsTheSettings()
    {
        var settings = new OpenIdServerSettings
        {
            AccessTokenFormat = OpenIdServerSettings.TokenFormat.DataProtection,
            Authority = new Uri("https://login.example.com/"),
            AuthorizationEndpointPath = "/connect/authorize",
            TokenEndpointPath = "/connect/token",
            AllowAuthorizationCodeFlow = true,
            AllowRefreshTokenFlow = true,
            SigningCertificateStoreLocation = StoreLocation.LocalMachine,
            SigningCertificateStoreName = StoreName.My,
            SigningCertificateThumbprint = "ABC",
            RequireEndSessionConfirmation = false,
        };

        var form = OpenIdServerConfigurationApiController.ToModel(settings);
        var written = new OpenIdServerSettings();
        var errors = OpenIdServerConfigurationApiController.Apply(form, written);

        Assert.Empty(errors);
        Assert.Equal(settings.AccessTokenFormat, written.AccessTokenFormat);
        Assert.Equal(settings.Authority, written.Authority);
        Assert.Equal("/connect/authorize", written.AuthorizationEndpointPath.Value);
        Assert.Equal("/connect/token", written.TokenEndpointPath.Value);
        Assert.False(written.LogoutEndpointPath.HasValue);
        Assert.True(written.AllowAuthorizationCodeFlow);
        Assert.True(written.AllowRefreshTokenFlow);
        Assert.Equal(StoreLocation.LocalMachine, written.SigningCertificateStoreLocation);
        Assert.Equal(StoreName.My, written.SigningCertificateStoreName);
        Assert.Equal("ABC", written.SigningCertificateThumbprint);
        Assert.False(written.RequireEndSessionConfirmation);
    }

    [Theory]
    [InlineData("not a uri", "Authority")]
    [InlineData(null, null)]
    public void AnAuthorityMustBeAbsolute(string authority, string expectedErrorKey)
    {
        var form = new OpenIdServerConfigurationForm { Authority = authority };

        var errors = OpenIdServerConfigurationApiController.Apply(form, new OpenIdServerSettings());

        if (expectedErrorKey is null)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.Contains(errors, error => error.Key == expectedErrorKey);
        }
    }

    [Fact]
    public void AnUnknownTokenFormatIsRefused()
    {
        var errors = OpenIdServerConfigurationApiController.Apply(new OpenIdServerConfigurationForm { AccessTokenFormat = "Paper" }, new OpenIdServerSettings());

        Assert.Contains(errors, error => error.Key == nameof(OpenIdServerConfigurationForm.AccessTokenFormat));
    }
}
