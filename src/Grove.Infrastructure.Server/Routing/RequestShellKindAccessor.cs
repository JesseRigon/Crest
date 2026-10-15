using Grove.Infrastructure.Shells.Admin;
using Grove.Display.Management.Components;
using Grove.Extensions;
using Microsoft.AspNetCore.Http;

namespace Grove.Routing;

/// <summary>
/// The shell of the current request for the scoped component registry: the bucket
/// ShellSelectionMiddleware stamped (admin, member), else the admin shell for a request marked
/// as an admin request (<see cref="AdminRequest"/>), else the site shell, the fallback bucket.
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

            if (context.Items.TryGetValue(BlazorHosting.ShellBucketItem, out var stamped) && stamped is ShellId bucket)
            {
                return ToShellKind(bucket);
            }

            return AdminRequest.IsApplied(context) ? ShellKind.Admin : ShellKind.Site;
        }
    }

    public static ShellKind ToShellKind(ShellId bucket) => ComponentSchema.ParseShell(bucket.Value) ?? ShellKind.Site;
}
