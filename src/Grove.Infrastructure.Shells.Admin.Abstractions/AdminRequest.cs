using Microsoft.AspNetCore.Http;

namespace Grove.Infrastructure.Shells.Admin;

/// <summary>
/// The admin request marker: the shell selection marks a request of the admin shell, and the
/// admin theme selector and the shell-kind accessor read it.
/// </summary>
public static class AdminRequest
{
    public static void Apply(HttpContext context)
    {
        // The value isn't important, it's just a marker object
        context.Items[typeof(AdminRequest)] = null;
    }

    public static bool IsApplied(HttpContext context)
        => context.Items.ContainsKey(typeof(AdminRequest));
}
