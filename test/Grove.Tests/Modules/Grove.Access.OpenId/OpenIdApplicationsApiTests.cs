using Grove.Access.OpenId.Abstractions.Managers;
using Microsoft.AspNetCore.Http.HttpResults;
using Grove.Access.OpenId.Api;

namespace Grove.Tests.Modules.Grove.OpenId;

// The OpenID applications API's handlers: the application rules, answered as a JSON refusal
// (a 400 with an OpenIdSaveResult body).
public class OpenIdApplicationsApiTests
{
    [Fact]
    public async Task CreatingIsForbiddenWithoutTheManageApplicationsPermission()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        var handlers = Handlers(authorization.Object);

        var result = await handlers.CreateAsync(new OpenIdApplicationModel { ClientId = "client", Type = OpenIddictConstants.ClientTypes.Public });

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Theory]
    [InlineData(OpenIddictConstants.ClientTypes.Public, "ClientSecret", false)]
    [InlineData(OpenIddictConstants.ClientTypes.Public, "", true)]
    [InlineData(OpenIddictConstants.ClientTypes.Confidential, "ClientSecret", true)]
    [InlineData(OpenIddictConstants.ClientTypes.Confidential, "", false)]
    public async Task AConfidentialClientNeedsASecretAndAPublicOneTakesNone(string clientType, string clientSecret, bool expectSaved)
    {
        var handlers = Handlers(Allow());

        var result = await handlers.CreateAsync(new OpenIdApplicationModel
        {
            ClientId = "client",
            Type = clientType,
            ClientSecret = clientSecret,
            AllowAuthorizationCodeFlow = true,
        });

        AssertSaved(expectSaved, result, nameof(OpenIdApplicationModel.ClientSecret));
    }

    [Theory]
    [InlineData("nonUrlString", false)]
    [InlineData("http://localhost http://localhost:8080 nonUrlString", false)]
    [InlineData("http://localhost#fragment", false)]
    [InlineData("http://localhost http://localhost:8080", true)]
    public async Task RedirectUrisMustBeAbsoluteWithoutAFragment(string uris, bool expectSaved)
    {
        var handlers = Handlers(Allow());

        var result = await handlers.CreateAsync(new OpenIdApplicationModel
        {
            ClientId = "123",
            DisplayName = "Name",
            Type = OpenIddictConstants.ClientTypes.Public,
            AllowAuthorizationCodeFlow = true,
            RedirectUris = uris,
        });

        AssertSaved(expectSaved, result, nameof(OpenIdApplicationModel.RedirectUris));
    }

    [Fact]
    public async Task ATakenClientIdIsRefused()
    {
        var applications = new Mock<IOpenIdApplicationManager>();
        applications.Setup(manager => manager.FindByClientIdAsync("taken", It.IsAny<CancellationToken>())).Returns(new ValueTask<object>(new object()));
        var handlers = Handlers(Allow(), applications.Object);

        var result = await handlers.CreateAsync(new OpenIdApplicationModel { ClientId = "taken", Type = OpenIddictConstants.ClientTypes.Public });

        AssertSaved(false, result, nameof(OpenIdApplicationModel.ClientId));
    }

    [Fact]
    public void AnEmptySecretKeepsTheStoredOne()
    {
        var settings = OpenIdApplicationsHandlers.ToSettings(new OpenIdApplicationModel { ClientId = "client", Type = OpenIddictConstants.ClientTypes.Confidential, ClientSecret = "" });

        Assert.Null(settings.ClientSecret);
    }

    private static void AssertSaved(bool expectSaved, IResult result, string field)
    {
        var json = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        if (expectSaved)
        {
            Assert.True(status.StatusCode is null or StatusCodes.Status200OK);
            Assert.True(Assert.IsType<OpenIdSaveResult>(json.Value).Succeeded);
        }
        else
        {
            Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
            var save = Assert.IsType<OpenIdSaveResult>(json.Value);
            Assert.False(save.Succeeded);
            Assert.Contains(save.Errors, error => error.Key == field);
        }
    }

    private static IAuthorizationService Allow()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authorization.Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        return authorization.Object;
    }

    private static OpenIdApplicationsHandlers Handlers(IAuthorizationService authorization, IOpenIdApplicationManager? applications = null)
    {
        var localizer = new Mock<IStringLocalizer<OpenIdApplicationsHandlers>>();
        localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        var handlers = new OpenIdApplicationsHandlers(
            authorization,
            applications ?? Mock.Of<IOpenIdApplicationManager>(),
            Mock.Of<IOpenIdScopeManager>(),
            localizer.Object);
        handlers.BindTo(new DefaultHttpContext { User = new ClaimsPrincipal() });
        return handlers;
    }
}
