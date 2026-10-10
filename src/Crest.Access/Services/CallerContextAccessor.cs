using Crest.Environment.Shell.Scope;

namespace Crest.Access.Services;

/// <summary>The caller as a shell-scope feature, so a scope created from this one (a child
/// scope, a deferred task, after-commit work) carries the same caller without rebuilding it.</summary>
public sealed class CallerFeature(CallerContext caller) : IInheritedShellScopeFeature
{
    public CallerContext Caller { get; } = caller;
}

/// <summary>
/// Scoped per shell scope: one caller per request, burst or job. Set by the gate or a
/// background entry point; a scope that inherited its parent's caller answers with it.
/// </summary>
public sealed class CallerContextAccessor : ICallerContextAccessor
{
    private CallerContext? _current;

    public CallerContext? Current
    {
        get => _current ??= ShellScope.GetFeature<CallerFeature>()?.Caller;
        set
        {
            _current = value;
            if (value is not null)
            {
                ShellScope.SetFeature(new CallerFeature(value));
            }
        }
    }
}
