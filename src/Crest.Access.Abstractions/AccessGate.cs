using Microsoft.AspNetCore.Http;

namespace Crest.Access;

/// <summary>
/// What the request path stamps and the gate reads: the shell bucket and the caller, on
/// <see cref="HttpContext.Items"/>. The shell selector (the host's middleware) stamps the
/// bucket; the gate builds the caller from it.
/// </summary>
public static class AccessGate
{
    /// <summary>Items key: the <see cref="CallerSide"/> the shell selector chose, boxed.</summary>
    public const string SideItem = "Crest.Access.Side";

    public static CallerSide SideOf(HttpContext? context)
    {
        if (context?.Items.TryGetValue(SideItem, out var side) == true && side is CallerSide value)
        {
            return value;
        }

        return CallerSide.Site;
    }

    public static bool TryParseSide(string? value, out CallerSide side)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "admin":
                side = CallerSide.Admin;
                return true;
            case "member":
                side = CallerSide.Member;
                return true;
            case "site":
                side = CallerSide.Site;
                return true;
            default:
                side = CallerSide.Site;
                return false;
        }
    }
}
