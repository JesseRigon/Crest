using Grove.Access;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Liquid;

/// <summary>
/// The data scope of the user-loading Liquid filters: a template sees the caller's own
/// account, and other accounts only when the caller may view user profiles. With no
/// caller nothing loads (fail-closed). Tenant-authored templates must not be a way to
/// enumerate the user store (docs/content-items.md › Fluid sandboxing).
/// </summary>
internal static class LiquidUserScope
{
    public static async Task<IReadOnlyList<User>> VisibleAsync(IEnumerable<User> users, IAccessDecision decision, ICallerContextAccessor callers)
    {
        var caller = callers.Current;
        if (caller is null)
        {
            return [];
        }

        var list = users.ToList();
        if (list.All(user => string.Equals(user.UserId, caller.UserId, StringComparison.Ordinal)))
        {
            return list;
        }

        var mayViewOthers = await decision.AllowsAsync(caller, UsersPermissions.ViewUsers.Name);
        return [.. list.Where(user => mayViewOthers || string.Equals(user.UserId, caller.UserId, StringComparison.Ordinal))];
    }
}
