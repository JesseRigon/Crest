using Crest.OpenId.Abstractions.Managers;
using Crest.OpenId.Controllers;

namespace Crest.Tests.Modules.Crest.OpenId;

// The OpenID applications API (step 7: it replaces the MVC ApplicationController the earlier
// tests covered): the same rules, answered as a JSON refusal instead of a redisplayed form.
public class OpenIdApplicationsApiTests
{
    [Fact]
    public async Task CreatingIsForbiddenWithoutTheManageApplicationsPermission()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        var controller = Controller(authorization.Object);

        var result = await controller.CreateAsync(new OpenIdApplicationModel { ClientId = "client", Type = OpenIddictConstants.ClientTypes.Public });

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Theory]
    [InlineData(OpenIddictConstants.ClientTypes.Public, "ClientSecret", false)]
    [InlineData(OpenIddictConstants.ClientTypes.Public, "", true)]
    [InlineData(OpenIddictConstants.ClientTypes.Confidential, "ClientSecret", true)]
    [InlineData(OpenIddictConstants.ClientTypes.Confidential, "", false)]
    public async Task AConfidentialClientNeedsASecretAndAPublicOneTakesNone(string clientType, string clientSecret, bool expectSaved)
    {
        var controller = Controller(Allow());

        var result = await controller.CreateAsync(new OpenIdApplicationModel
        {
            ClientId = "client",
            Type = clientType,
            ClientSecret = clientSecret,
            AllowAuthorizationCodeFlow = true,
        });

        AssertSaved(expectSaved, result.Result, nameof(OpenIdApplicationModel.ClientSecret));
    }

    [Theory]
    [InlineData("nonUrlString", false)]
    [InlineData("http://localhost http://localhost:8080 nonUrlString", false)]
    [InlineData("http://localhost#fragment", false)]
    [InlineData("http://localhost http://localhost:8080", true)]
    public async Task RedirectUrisMustBeAbsoluteWithoutAFragment(string uris, bool expectSaved)
    {
        var controller = Controller(Allow());

        var result = await controller.CreateAsync(new OpenIdApplicationModel
        {
            ClientId = "123",
            DisplayName = "Name",
            Type = OpenIddictConstants.ClientTypes.Public,
            AllowAuthorizationCodeFlow = true,
            RedirectUris = uris,
        });

        AssertSaved(expectSaved, result.Result, nameof(OpenIdApplicationModel.RedirectUris));
    }

    [Fact]
    public async Task ATakenClientIdIsRefused()
    {
        var applications = new Mock<IOpenIdApplicationManager>();
        applications.Setup(manager => manager.FindByClientIdAsync("taken", It.IsAny<CancellationToken>())).Returns(new ValueTask<object>(new object()));
        var controller = Controller(Allow(), applications.Object);

        var result = await controller.CreateAsync(new OpenIdApplicationModel { ClientId = "taken", Type = OpenIddictConstants.ClientTypes.Public });

        AssertSaved(false, result.Result, nameof(OpenIdApplicationModel.ClientId));
    }

    [Fact]
    public void AnEmptySecretKeepsTheStoredOne()
    {
        var settings = OpenIdApplicationsApiController.ToSettings(new OpenIdApplicationModel { ClientId = "client", Type = OpenIddictConstants.ClientTypes.Confidential, ClientSecret = "" });

        Assert.Null(settings.ClientSecret);
    }

    private static void AssertSaved(bool expectSaved, IActionResult? result, string field)
    {
        if (expectSaved)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.True(Assert.IsType<OpenIdSaveResult>(ok.Value).Succeeded);
        }
        else
        {
            var refused = Assert.IsType<BadRequestObjectResult>(result);
            var save = Assert.IsType<OpenIdSaveResult>(refused.Value);
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

    private static OpenIdApplicationsApiController Controller(IAuthorizationService authorization, IOpenIdApplicationManager? applications = null)
    {
        var localizer = new Mock<IStringLocalizer<OpenIdApplicationsApiController>>();
        localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        return new OpenIdApplicationsApiController(
            authorization,
            applications ?? Mock.Of<IOpenIdApplicationManager>(),
            Mock.Of<IOpenIdScopeManager>(),
            localizer.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } },
        };
    }
}
