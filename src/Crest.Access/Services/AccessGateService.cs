using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crest.Access.Services;

/// <summary>The one gate: see <see cref="IAccessGate"/>.</summary>
public sealed class AccessGateService(ILogger<AccessGateService> logger) : IAccessGate
{
    /// <summary>Items key, set once the gate ran for the request (the caller may still be anonymous).</summary>
    public const string GatedItem = "Crest.Access.Gated";

    public async Task<bool> AdmitAsync(HttpContext context, CallerSide side, bool allowAnonymous, string? organizationId = null)
    {
        if (!context.Items.ContainsKey(GatedItem))
        {
            await AuthenticateOnceAsync(context);
        }

        context.Items[AccessGate.SideItem] = side;
        var requestedOrganization = organizationId ?? context.Request.Headers[AccessHeaders.Organization].ToString();

        try
        {
            var factory = context.RequestServices.GetRequiredService<ICallerContextFactory>();
            var caller = await factory.CreateAsync(
                new CallerRequest(context.User, side, string.IsNullOrEmpty(requestedOrganization) ? null : requestedOrganization, CultureInfo.CurrentUICulture.Name),
                context.RequestAborted);
            context.RequestServices.GetRequiredService<ICallerContextAccessor>().Current = caller;
        }
        catch (CallerDeniedException denied)
        {
            logger.LogInformation("Request on {Path} refused by the caller factory: {Reason}", context.Request.Path, denied.Message);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync(denied.Message);
            return false;
        }

        return allowAnonymous || context.User.Identity?.IsAuthenticated == true;
    }

    public async Task<bool> AdmitApiAsync(HttpContext context, CallerSide? prefixSide)
    {
        var header = context.Request.Headers[AccessHeaders.Shell].ToString();
        if (AccessGate.TryParseSide(header, out var side))
        {
            if (prefixSide is { } prefix && prefix != side)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("X-Shell disagrees with the shell the request was addressed to.");
                return false;
            }

            return await AdmitAsync(context, side, allowAnonymous: true);
        }

        if (!string.IsNullOrEmpty(header))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Unknown X-Shell value.");
            return false;
        }

        // No header: authenticate first, so a cookie-authenticated call with no side can be
        // refused as malformed without a second authentication.
        await AuthenticateOnceAsync(context);
        var isCookie = context.User.Identity?.IsAuthenticated == true && !context.Request.Headers.ContainsKey("Authorization");
        if (isCookie && prefixSide is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("A cookie-authenticated API request must carry an X-Shell header.");
            return false;
        }

        return await AdmitAsync(context, prefixSide ?? CallerSide.Site, allowAnonymous: true);
    }

    /// <summary>
    /// The one authentication: the Api scheme when the request carries an Authorization
    /// header, else the identity cookie. The result is published as the standard
    /// authentication feature, so code that needs the ticket's properties (the member
    /// session's active organization, an impersonation) reads it instead of authenticating
    /// again.
    /// </summary>
    private static async Task AuthenticateOnceAsync(HttpContext context)
    {
        context.Items[GatedItem] = true;
        var scheme = context.Request.Headers.ContainsKey("Authorization")
            ? PlatformConstants.AuthenticationSchemes.Api
            : IdentityConstants.ApplicationScheme;
        var result = await context.AuthenticateAsync(scheme);
        var feature = new GatedAuthentication(result);
        context.Features.Set<IHttpAuthenticationFeature>(feature);
        context.Features.Set<IAuthenticateResultFeature>(feature);
        if (result.Succeeded && result.Principal is not null)
        {
            context.User = result.Principal;
        }
    }
}

/// <summary>The gate's authentication result, as the standard features read it.</summary>
internal sealed class GatedAuthentication(AuthenticateResult result) : IAuthenticateResultFeature, IHttpAuthenticationFeature
{
    private AuthenticateResult? _result = result;

    public AuthenticateResult? AuthenticateResult
    {
        get => _result;
        set
        {
            _result = value;
            User = value?.Principal;
        }
    }

    public System.Security.Claims.ClaimsPrincipal? User { get; set; } = result.Principal;
}

/// <summary>
/// The gate at the platform's authentication slot, for requests the shell selector did not
/// classify (a host without the selector, assets, infrastructure paths): runs the
/// request-handler schemes the authentication middleware used to run (OpenIddict's own
/// endpoints are such a scheme), then admits the request as Site - an API call by its path,
/// a page anonymously - when no gate ran before. A background run of the tenant pipeline
/// carries the system caller its entry point set and is left alone.
/// </summary>
public sealed class AccessGateMiddleware(RequestDelegate next, IAuthenticationSchemeProvider schemes)
{
    public async Task InvokeAsync(HttpContext context, IAccessGate gate, ICallerContextAccessor accessor)
    {
        if (!context.Items.ContainsKey("IsBackground"))
        {
            var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            foreach (var scheme in await schemes.GetRequestHandlerSchemesAsync())
            {
                if (await handlers.GetHandlerAsync(context, scheme.Name) is IAuthenticationRequestHandler handler && await handler.HandleRequestAsync())
                {
                    return;
                }
            }

            if (accessor.Current is null)
            {
                var path = context.Request.Path;
                var isApi = path.StartsWithSegments("/api") || path.StartsWithSegments("/crest-workflows");
                if (isApi ? !await gate.AdmitApiAsync(context, prefixSide: null) : !await gate.AdmitAsync(context, CallerSide.Site, allowAnonymous: true))
                {
                    return;
                }
            }
        }

        await next(context);
    }
}
