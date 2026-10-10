using Microsoft.Extensions.Primitives;
using Crest.Environment.Shell.Scope;
using Crest.Modules;

namespace Crest.Environment.Cache;

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
