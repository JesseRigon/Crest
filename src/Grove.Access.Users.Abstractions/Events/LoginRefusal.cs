#nullable enable

namespace Grove.Access.Users.Events;

/// <summary>
/// A login event's refusal (<see cref="ILoginFormEvent.ValidatingLoginAsync"/>) or detour
/// (<see cref="ILoginFormEvent.LoggingInAsync()"/>), and the payload a login surface answers a
/// refused sign-in with: the human-readable reasons, and the page to go to instead (null when
/// the surface shows the reasons itself). A redirect starting with <c>~/</c> is relative to the
/// tenant; the login surface resolves it against the request's path base.
/// </summary>
public sealed record LoginRefusal(IReadOnlyList<string> Errors, string? Redirect = null)
{
    /// <summary>A refusal with one reason, and optionally the page that explains it.</summary>
    public static LoginRefusal Because(string message, string? redirect = null) => new([message], redirect);

    /// <summary>Sends the browser to <paramref name="redirect"/> (a page that explains, or a challenge).</summary>
    public static LoginRefusal RedirectTo(string redirect) => new([], redirect);
}
