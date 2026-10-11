using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Routing;

namespace Crest.Media.Services;

public class SecureMediaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PathString _assetsRequestPath;

    public SecureMediaMiddleware(
        RequestDelegate next,
        IOptions<MediaOptions> mediaOptions)
    {
        _next = next;
        _assetsRequestPath = mediaOptions.Value.AssetsRequestPath;
    }

    public Task Invoke(HttpContext context, IAuthorizationService authorizationService, IAuthenticationService authenticationService)
    {
        var validateAssetsRequestPath = context.Request.Path.StartsWithNormalizedSegments(_assetsRequestPath, StringComparison.OrdinalIgnoreCase, out var subPath);
        if (!validateAssetsRequestPath)
        {
            return _next(context);
        }

        return Awaited(context, authorizationService, authenticationService, _next, subPath);

        static async Task Awaited(
            HttpContext context,
            IAuthorizationService authorizationService,
            IAuthenticationService authenticationService,
            RequestDelegate next,
            PathString subPath)
        {
            // The request path's gate authenticated the request (bearer or cookie); the
            // principal is the gate's.
            if (await authorizationService.AuthorizeAsync(context.User, MediaPermissions.ViewMedia, (object)subPath.ToString()))
            {
                // A file an anonymous caller could not see is served with the secure caching
                // policy; one anyone may see keeps the default browser caching.
                var callers = context.RequestServices.GetRequiredService<Crest.Access.ICallerContextAccessor>();
                if (callers.Current is { IsAuthenticated: true } caller)
                {
                    var decision = context.RequestServices.GetRequiredService<Crest.Access.IAccessDecision>();
                    var anonymous = Crest.Access.CallerContext.Anonymous(caller.Tenant, caller.Side, permissionVersion: caller.PermissionVersion);
                    if (!(await decision.DecideAsync(anonymous, MediaPermissions.ViewMedia.Name, subPath.ToString())).IsAllowed)
                    {
                        context.MarkAsSecureMediaRequested();
                    }
                }

                await next(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }
        }
    }
}
