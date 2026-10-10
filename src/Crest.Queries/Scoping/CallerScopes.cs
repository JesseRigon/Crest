#nullable enable
using Crest.Access;

namespace Crest.Queries;

/// <summary>
/// The caller's scope for a query run. Nothing runs unfiltered: no caller, no provider or no
/// rule for a target is a refusal.
/// </summary>
public static class CallerScopes
{
    public static async Task<ScopeSet> RequireAsync(ICallerContextAccessor? callerAccessor, IScopeSetProvider? scopeSetProvider, string target, CancellationToken cancellationToken)
    {
        var caller = callerAccessor?.Current;

        if (caller is null || scopeSetProvider is null)
        {
            throw new ScopeRefusedException(target);
        }

        return await scopeSetProvider.GetAsync(caller, cancellationToken);
    }
}
