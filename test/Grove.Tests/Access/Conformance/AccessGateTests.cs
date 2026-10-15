using System.Security.Claims;
using Grove.Access;
using Grove.Access.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grove.Tests.Access.Conformance;

/// <summary>
/// The one gate (docs/access.md § One gate): it authenticates once per request, the shell
/// prefix decides an API call's side and <c>X-Shell</c> may only confirm it, a cookie call
/// with no side is malformed, a contributor's refusal is a 403, and the caller it builds is
/// the one every later step reads.
/// </summary>
public class AccessGateTests
{
    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext Current { get; set; }
    }

    private sealed class Harness
    {
        public Accessor Accessor { get; } = new();

        public List<string> AuthenticatedSchemes { get; } = [];

        public List<CallerRequest> Built { get; } = [];

        public ClaimsPrincipal Principal { get; set; }

        public string Denial { get; set; }

        public DefaultHttpContext Context(string authorization = null, string shell = null, string organization = null)
        {
            var authentication = new Mock<IAuthenticationService>();
            authentication
                .Setup(m => m.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
                .ReturnsAsync((HttpContext _, string scheme) =>
                {
                    AuthenticatedSchemes.Add(scheme);
                    return Principal is null ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(Principal, scheme));
                });

            var factory = new Mock<ICallerContextFactory>();
            factory
                .Setup(m => m.CreateAsync(It.IsAny<CallerRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallerRequest request, CancellationToken _) =>
                {
                    Built.Add(request);
                    if (Denial is not null)
                    {
                        throw new CallerDeniedException(Denial);
                    }

                    return new CallerContext
                    {
                        Tenant = AccessWorld.Tenant,
                        Side = request.Side,
                        UserId = request.Principal?.Identity?.IsAuthenticated == true ? request.Principal.FindFirstValue(ClaimTypes.NameIdentifier) : null,
                        OrganizationId = request.RequestedOrganizationId,
                    };
                });

            var services = new ServiceCollection();
            services.AddSingleton(authentication.Object);
            services.AddSingleton(factory.Object);
            services.AddSingleton<ICallerContextAccessor>(Accessor);

            var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
            context.Response.Body = new MemoryStream();
            if (authorization is not null)
            {
                context.Request.Headers.Authorization = authorization;
            }

            if (shell is not null)
            {
                context.Request.Headers[AccessHeaders.Shell] = shell;
            }

            if (organization is not null)
            {
                context.Request.Headers[AccessHeaders.Organization] = organization;
            }

            return context;
        }
    }

    private static AccessGateService Gate() => new(NullLogger<AccessGateService>.Instance);

    private static ClaimsPrincipal Cookie(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], IdentityConstants.ApplicationScheme));

    [Fact]
    public async Task ThePrefixDecidesAndAConfirmingHeaderIsAdmitted()
    {
        var harness = new Harness { Principal = Cookie("u-1") };
        var context = harness.Context(shell: "admin");

        Assert.True(await Gate().AdmitApiAsync(context, CallerSide.Admin));
        Assert.Equal(CallerSide.Admin, harness.Accessor.Current.Side);
        Assert.Equal(CallerSide.Admin, AccessGate.SideOf(context));
    }

    [Fact]
    public async Task AHeaderThatDisagreesWithThePrefixIsForbidden()
    {
        var harness = new Harness { Principal = Cookie("u-1") };
        var context = harness.Context(shell: "admin");

        Assert.False(await Gate().AdmitApiAsync(context, CallerSide.Member));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(harness.Accessor.Current);
        Assert.Empty(harness.Built);
    }

    [Fact]
    public async Task AnUnknownShellHeaderIsABadRequest()
    {
        var harness = new Harness();
        var context = harness.Context(shell: "root");

        Assert.False(await Gate().AdmitApiAsync(context, null));
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task ASystemShellHeaderIsRefused()
    {
        // No request may name the system side; it exists only for in-process entry points.
        var harness = new Harness();
        var context = harness.Context(shell: "system");

        Assert.False(await Gate().AdmitApiAsync(context, null));
        Assert.NotEqual(CallerSide.System, harness.Accessor.Current?.Side);
    }

    [Fact]
    public async Task ACookieCallWithNeitherPrefixNorHeaderIsMalformed()
    {
        var harness = new Harness { Principal = Cookie("u-1") };
        var context = harness.Context();

        Assert.False(await Gate().AdmitApiAsync(context, null));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ABearerCallWithNoSideIsOnTheSite()
    {
        var harness = new Harness { Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "app-1")], "Bearer")) };
        var context = harness.Context(authorization: "Bearer abc");

        Assert.True(await Gate().AdmitApiAsync(context, null));
        Assert.Equal(CallerSide.Site, harness.Accessor.Current.Side);
        Assert.Equal("app-1", harness.Accessor.Current.UserId);
    }

    [Fact]
    public async Task AnAuthorizationHeaderAuthenticatesTheApiSchemeAndNothingElse()
    {
        var harness = new Harness();
        var context = harness.Context(authorization: "Bearer abc");

        await Gate().AdmitApiAsync(context, null);

        Assert.Equal(new[] { PlatformConstants.AuthenticationSchemes.Api }, harness.AuthenticatedSchemes);
    }

    [Fact]
    public async Task WithoutAnAuthorizationHeaderTheCookieIsTheOnlyScheme()
    {
        var harness = new Harness();
        var context = harness.Context();

        await Gate().AdmitAsync(context, CallerSide.Site, allowAnonymous: true);

        Assert.Equal(new[] { IdentityConstants.ApplicationScheme }, harness.AuthenticatedSchemes);
    }

    [Fact]
    public async Task ARequestIsAuthenticatedOnceHoweverOftenItIsAdmitted()
    {
        var harness = new Harness { Principal = Cookie("u-1") };
        var context = harness.Context();
        var gate = Gate();

        await gate.AdmitAsync(context, CallerSide.Site, allowAnonymous: true);
        await gate.AdmitAsync(context, CallerSide.Admin, allowAnonymous: false);

        Assert.Single(harness.AuthenticatedSchemes);
    }

    [Fact]
    public async Task TheAuthenticationResultIsPublishedForLaterReaders()
    {
        var harness = new Harness { Principal = Cookie("u-1") };
        var context = harness.Context();

        await Gate().AdmitAsync(context, CallerSide.Admin, allowAnonymous: false);

        var feature = context.Features.Get<IAuthenticateResultFeature>();
        Assert.True(feature?.AuthenticateResult?.Succeeded);
        Assert.Equal("u-1", context.User.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task AnAnonymousRequestWhereAnonymousIsNotAllowedIsRefusedWithoutAResponse()
    {
        // The caller (the shell selector) chooses between a login redirect and a 401.
        var harness = new Harness();
        var context = harness.Context();

        Assert.False(await Gate().AdmitAsync(context, CallerSide.Admin, allowAnonymous: false));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.NotNull(harness.Accessor.Current);
        Assert.False(harness.Accessor.Current.IsAuthenticated);
    }

    [Fact]
    public async Task AContributorsRefusalIsAForbiddenAndNoCallerIsSet()
    {
        var harness = new Harness { Principal = Cookie("u-1"), Denial = "No binding to org-9." };
        var context = harness.Context(organization: "org-9");

        Assert.False(await Gate().AdmitAsync(context, CallerSide.Member, allowAnonymous: false));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(harness.Accessor.Current);
    }

    [Fact]
    public async Task TheRequestedOrganizationComesFromTheHeaderUnlessTheSelectorNamedOne()
    {
        var harness = new Harness { Principal = Cookie("u-1") };

        await Gate().AdmitAsync(harness.Context(organization: "org-header"), CallerSide.Member, allowAnonymous: false);
        await Gate().AdmitAsync(harness.Context(organization: "org-header"), CallerSide.Member, allowAnonymous: false, organizationId: "org-path");

        Assert.Equal("org-header", harness.Built[0].RequestedOrganizationId);
        Assert.Equal("org-path", harness.Built[1].RequestedOrganizationId);
    }
}
