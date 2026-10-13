using Crest.Admin;
using Crest.DisplayManagement.Components;
using Crest.Extensions;
using Microsoft.AspNetCore.Http;

namespace Crest.Routing;

/// <summary>
/// The shell of the current request for the scoped component registry: the bucket
/// BlazorAdminThemeMiddleware stamped (admin, member), else the admin shell for an MVC admin
/// request, else the site shell, the fallback bucket.
/// </summary>
public sealed class RequestShellKindAccessor(IHttpContextAccessor httpContextAccessor) : IShellKindAccessor
{
    public ShellKind ShellKind
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                return ShellKind.Site;
            }

            if (context.Items.TryGetValue(CrestBlazorHosting.ShellBucketItem, out var stamped) && stamped is RouteBucket bucket)
            {
                return ToShellKind(bucket);
            }

            return AdminAttribute.IsApplied(context) ? ShellKind.Admin : ShellKind.Site;
        }
    }

    public static ShellKind ToShellKind(RouteBucket bucket) => bucket switch
    {
        RouteBucket.Admin => ShellKind.Admin,
        RouteBucket.Member => ShellKind.Member,
        _ => ShellKind.Site,
    };
}
