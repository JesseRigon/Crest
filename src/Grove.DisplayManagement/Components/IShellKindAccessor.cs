#nullable enable

namespace Grove.DisplayManagement.Components;

/// <summary>
/// The shell the current request renders in, which picks the scoped <see cref="IComponentRegistry"/>
/// (§ 1: a registry per shell). The platform default is the site shell; a host that serves the
/// admin and member shells replaces it (Grove.Server reads the request's shell bucket).
/// </summary>
public interface IShellKindAccessor
{
    ShellKind ShellKind { get; }
}

/// <summary>The site shell, for hosts that serve no other.</summary>
public sealed class SiteShellKindAccessor : IShellKindAccessor
{
    public ShellKind ShellKind => ShellKind.Site;
}
