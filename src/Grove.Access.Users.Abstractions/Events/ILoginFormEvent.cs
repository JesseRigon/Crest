#nullable enable

namespace Grove.Access.Users.Events;

/// <summary>
/// Contract for login events.
/// </summary>
public interface ILoginFormEvent
{
    /// <summary>
    /// Occurs when the user login has failed and the user was not found.
    /// </summary>
    /// <param name="userName">The username.</param>
    Task LoggingInFailedAsync(string userName);

    /// <summary>
    /// Occurs when the user login has failed and the user is known.
    /// </summary>
    /// <param name="user">The <see cref="IUser"/>.</param>
    Task LoggingInFailedAsync(IUser user);

    /// <summary>
    /// Occurs when a user is locked out.
    /// </summary>
    /// <param name="user">The <see cref="IUser"/>.</param>
    Task IsLockedOutAsync(IUser user);

    /// <summary>
    /// Occurs when the user is logged in.
    /// </summary>
    /// <param name="user">The <see cref="IUser"/>.</param>
    Task LoggedInAsync(IUser user);

    /// <summary>
    /// Occurs when the user is logging in.
    /// </summary>
    /// <param name="userName">The username.</param>
    /// <param name="reportError">The reported error if failure happened during the login process.</param>
    Task LoggingInAsync(string userName, Action<string, string> reportError);

    /// <summary>
    /// Occurs when a user visits the login page. A non-null answer sends the browser to its
    /// <see cref="LoginRefusal.Redirect"/> instead of showing the form (the only external
    /// provider, say).
    /// </summary>
    Task<LoginRefusal?> LoggingInAsync();

    /// <summary>
    /// Occurs when a user is trying to login and after the user is located. A non-null answer
    /// refuses the sign-in: its messages are shown on the login surface, and its
    /// <see cref="LoginRefusal.Redirect"/>, when set, is the page the browser goes to instead.
    /// </summary>
    Task<LoginRefusal?> ValidatingLoginAsync(IUser user);
}
