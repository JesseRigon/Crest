namespace Crest.Environment.Shell.Scope;

/// <summary>
/// A shared feature of a <see cref="ShellScope"/> that is handed down to every scope created
/// from it: child scopes, deferred tasks and after-commit work. The one access calculation a
/// request makes travels this way through everything the request causes on the server, and
/// never further: a new request, a resumed burst or a scheduled run starts its own scope with
/// no parent.
/// </summary>
public interface IInheritedShellScopeFeature
{
}
