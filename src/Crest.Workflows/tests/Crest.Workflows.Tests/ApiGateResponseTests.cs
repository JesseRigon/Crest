using System.Security.Claims;
using Crest.Access;
using Crest.Access.Services;
using Crest.Workflows.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// The engine API behind the one gate (docs/access.md § One gate, docs/workflows.md › The
// engine API gate). Two faults of 2026-10-13 pinned: the per-request grant the API gate puts on
// the principal must survive the authorization middleware, which re-reads and writes back the
// gate's authentication feature; and an API path answers 401 or 403 where a page is redirected
// to the login or the access-denied page.
public class ApiGateResponseTests
{
    private const string Cookie = "Identity.Application";
    private const string Policy = "epPolicy:features";

    private static readonly CallerContext Alice = new() { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice" };

    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; }
    }

    private static ClaimsPrincipal CookiePrincipal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u-1"), new Claim(ClaimTypes.Name, "alice")], Cookie));

    /// <summary>A request the gate admitted: authenticated once through the cookie, the caller built, the gate's feature in place.</summary>
    private static async Task<(DefaultHttpContext Context, IAuthenticationService Authentication)> AdmittedAsync(string path)
    {
        var authentication = Substitute.For<IAuthenticationService>();
        authentication.AuthenticateAsync(Arg.Any<HttpContext>(), Cookie)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(CookiePrincipal(), Cookie)));

        var factory = Substitute.For<ICallerContextFactory>();
        factory.CreateAsync(Arg.Any<CallerRequest>(), Arg.Any<CancellationToken>()).Returns(Alice);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(authentication)
            .AddSingleton(factory)
            .AddSingleton<ICallerContextAccessor>(new Accessor())
            .AddAuthorization(options => options.AddPolicy(Policy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.HasClaim(ApiSecurityMiddleware.PermissionsClaimType, "read:installed-features"))))
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Request.Headers[AccessHeaders.Shell] = "admin";

        Assert.True(await new AccessGateService(NullLogger<AccessGateService>.Instance).AdmitApiAsync(context, prefixSide: null));
        return (context, authentication);
    }

    /// <summary>What the API gate does once it decided: the engine's grant on the principal, for this request only.</summary>
    private static void Grant(HttpContext context, params string[] names)
    {
        context.User = new ClaimsPrincipal(context.User.Identities);
        context.User.AddIdentity(new ClaimsIdentity(names.Select(name => new Claim(ApiSecurityMiddleware.PermissionsClaimType, name)), ApiSecurityMiddleware.GrantAuthenticationType));
    }

    [Fact]
    public async Task The_grant_survives_the_authorization_middleware()
    {
        var (context, authentication) = await AdmittedAsync("/crest-workflows/api/features/installed");
        Grant(context, "read:*", "read:installed-features");
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AuthorizeAttribute { Policy = Policy }), "features"));

        var reached = false;
        var middleware = new AuthorizationMiddleware(_ => { reached = true; return Task.CompletedTask; }, context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>());
        await middleware.Invoke(context);

        Assert.True(reached);
        await authentication.DidNotReceiveWithAnyArgs().ForbidAsync(default!, default, default);
        await authentication.DidNotReceiveWithAnyArgs().ChallengeAsync(default!, default, default);
        Assert.Contains(context.User.Claims, claim => claim.Type == ApiSecurityMiddleware.PermissionsClaimType);
        Assert.Equal("alice", context.User.Identity?.Name);
    }

    [Fact]
    public async Task A_principal_set_after_the_gate_retires_the_ticket()
    {
        // The feature contract the middleware relies on (the platform's AuthenticationFeatures
        // has it): a principal set by hand retires the ticket; a ticket set afterwards carries
        // its principal.
        var (context, _) = await AdmittedAsync("/crest-workflows/api/features/installed");
        var feature = context.Features.Get<IAuthenticateResultFeature>()!;
        Assert.NotNull(feature.AuthenticateResult);

        Grant(context, "read:*");
        Assert.Null(feature.AuthenticateResult);
        Assert.Same(context.User, context.Features.Get<IHttpAuthenticationFeature>()!.User);

        var ticket = new AuthenticationTicket(context.User, "context.User");
        feature.AuthenticateResult = AuthenticateResult.Success(ticket);
        Assert.Same(ticket.Principal, context.User);
    }

    [Fact]
    public async Task Without_the_grant_the_middleware_still_forbids()
    {
        var (context, authentication) = await AdmittedAsync("/crest-workflows/api/features/installed");
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AuthorizeAttribute { Policy = Policy }), "features"));

        var reached = false;
        await new AuthorizationMiddleware(_ => { reached = true; return Task.CompletedTask; }, context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>()).Invoke(context);

        Assert.False(reached);
        await authentication.Received().ForbidAsync(context, Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Theory]
    [InlineData("/api/crest/workflows/registry", true)]
    [InlineData("/crest-workflows/api/features/installed", true)]
    [InlineData("/crest-workflows/hubs/workflow-instance", true)]
    [InlineData("/Features", false)]
    [InlineData("/apiary", false)]
    [InlineData("/", false)]
    public void An_api_path_is_the_tenant_root_api_and_the_engine(string path, bool isApi) =>
        Assert.Equal(isApi, AccessGate.IsApiPath(path));

    private static CookieAuthenticationOptions CookieOptions(string? scheme = null)
    {
        var options = new CookieAuthenticationOptions { Events = new CookieAuthenticationEvents() };
        new ApiCookieResponses().PostConfigure(scheme ?? IdentityConstants.ApplicationScheme, options);
        return options;
    }

    private static RedirectContext<CookieAuthenticationOptions> Redirect(CookieAuthenticationOptions options, string path, string redirectUri)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return new RedirectContext<CookieAuthenticationOptions>(context, new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)), options, new AuthenticationProperties(), redirectUri);
    }

    [Theory]
    [InlineData("/crest-workflows/api/features/installed")]
    [InlineData("/api/crest/workflows/registry")]
    public async Task An_api_path_answers_403_instead_of_the_access_denied_page(string path)
    {
        var options = CookieOptions();
        var redirect = Redirect(options, path, "/Error/403?ReturnUrl=" + Uri.EscapeDataString(path));

        await options.Events.OnRedirectToAccessDenied(redirect);

        Assert.Equal(StatusCodes.Status403Forbidden, redirect.Response.StatusCode);
        Assert.False(redirect.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData("/crest-workflows/api/features/installed")]
    [InlineData("/api/crest/workflows/registry")]
    public async Task An_api_path_answers_401_instead_of_the_login_page(string path)
    {
        var options = CookieOptions();
        var redirect = Redirect(options, path, "/Login?ReturnUrl=" + Uri.EscapeDataString(path));

        await options.Events.OnRedirectToLogin(redirect);

        Assert.Equal(StatusCodes.Status401Unauthorized, redirect.Response.StatusCode);
        Assert.False(redirect.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task A_page_keeps_its_redirects()
    {
        var options = CookieOptions();
        var denied = Redirect(options, "/Features", "/Error/403?ReturnUrl=%2FFeatures");
        var login = Redirect(options, "/Features", "/Login?ReturnUrl=%2FFeatures");

        await options.Events.OnRedirectToAccessDenied(denied);
        await options.Events.OnRedirectToLogin(login);

        Assert.Equal(StatusCodes.Status302Found, denied.Response.StatusCode);
        Assert.Equal("/Error/403?ReturnUrl=%2FFeatures", denied.Response.Headers.Location);
        Assert.Equal(StatusCodes.Status302Found, login.Response.StatusCode);
        Assert.Equal("/Login?ReturnUrl=%2FFeatures", login.Response.Headers.Location);
    }

    [Fact]
    public async Task Another_cookie_scheme_is_left_alone()
    {
        var options = CookieOptions(IdentityConstants.ExternalScheme);
        var redirect = Redirect(options, "/api/crest/x", "/Login");

        await options.Events.OnRedirectToLogin(redirect);

        Assert.Equal(StatusCodes.Status302Found, redirect.Response.StatusCode);
    }
}
