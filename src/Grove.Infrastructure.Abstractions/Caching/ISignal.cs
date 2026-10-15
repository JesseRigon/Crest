using Microsoft.Extensions.Primitives;
using Grove.Environment.Shell.Scope;
using Grove.Modules;

namespace Grove.Environment.Cache;

public interface ISignal : IModularTenantEvents
{
    IChangeToken GetToken(string key);
    Task SignalTokenAsync(string key);
}

public static class SignalExtensions
{
    /// <summary>
    /// Adds a Signal (if not already present) to be sent at the end of the shell scope.
    /// </summary>
    public static void DeferredSignalToken(this ISignal _, string key)
    {
        ShellScope.AddDeferredSignal(key);
    }
}
