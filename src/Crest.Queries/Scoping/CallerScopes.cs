#nullable enable
using Crest.Access;

namespace Crest.Queries;

/// <summary>
/// The caller's scope for a query run. Nothing runs unfiltered: no caller, no provider or no
/// rule for a target is a refusal.
/// </summary>
public static class CallerScopes
{
    /// <summary>
    /// The scope resolved when the query runs. Sources take this form: a scope provider can
    /// depend (through sharing, content or the organizations) on services that list the query
    /// sources, so resolving it while a source is constructed is a dependency cycle.
    /// </summary>
    public static Task<ScopeSet> RequireAsync(IServiceProvider services, string target, CancellationToken cancellationToken) =>
        RequireAsync(
            services.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor,
            services.GetService(typeof(IScopeSetProvider)) as IScopeSetProvider,
            target,
            cancellationToken);

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
