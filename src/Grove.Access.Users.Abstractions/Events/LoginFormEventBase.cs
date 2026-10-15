#nullable enable

namespace Grove.Access.Users.Events;

public abstract class LoginFormEventBase : ILoginFormEvent
{
    public virtual Task LoggingInAsync(string userName, Action<string, string> reportError)
        => Task.CompletedTask;

    public virtual Task LoggingInFailedAsync(string userName)
        => Task.CompletedTask;

    public virtual Task LoggingInFailedAsync(IUser user)
        => Task.CompletedTask;

    public virtual Task IsLockedOutAsync(IUser user)
        => Task.CompletedTask;

    public virtual Task LoggedInAsync(IUser user)
        => Task.CompletedTask;

    public virtual Task<LoginRefusal?> LoggingInAsync()
        => Task.FromResult<LoginRefusal?>(null);

    public virtual Task<LoginRefusal?> ValidatingLoginAsync(IUser user)
        => Task.FromResult<LoginRefusal?>(null);
}
