namespace Grove.Components.Modules;

/// <summary>The shells a client assembly can belong to.</summary>
/// <remarks>
/// Strings rather than an enum because the lazy-module generator reads the attribute out
/// of assembly metadata without loading the assembly, and a string argument is read
/// directly from the attribute blob. Server code carries them as <c>Grove.Routing.ShellId</c>.
/// </remarks>
public static class GroveShells
{
    public const string Admin = "admin";
    public const string Site = "site";
    public const string Member = "member";
}

/// <summary>
/// Declares which shell an assembly's pages belong to.
/// </summary>
/// <remarks>
/// A bucket is an ASSEMBLY property, never a per-page marker. Blazor's <c>Router</c> routes
/// every <c>[Route]</c> component in the assemblies it is given and has no per-type filter,
/// so if an admin page and a member page shared an assembly, both would be routable in both
/// shells on client-side navigation - past the server's route gate, which only sees full
/// page loads. A member could then reach an accounting page by following a link.
///
/// So a module ships one client library per shell it contributes to: admin pages in its
/// <c>blazor-wasm/</c> library, member pages in its <c>member-wasm/</c> library marked with
/// <c>[assembly: GroveShell(GroveShells.Member)]</c>, public pages in its <c>site-wasm/</c>
/// library marked with <c>[assembly: GroveShell(GroveShells.Site)]</c>. A module client
/// library without this attribute is admin, which is where module pages have always gone.
///
/// The same attribute is read everywhere the bucket matters - the server's route-table
/// providers, the endpoint bucket stamping, the lazy-module generator, each shell's router -
/// so none of them can disagree about where a page belongs.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GroveShellAttribute(string shell) : Attribute
{
    public string Shell { get; } = shell;

    /// <summary>
    /// On a theme client, the shell's <c>Routes</c> component: what the eager shell root
    /// (<c>Grove.Shell.GroveShellRoot</c>) renders once the theme client is loaded. A theme
    /// client is lazy - each loads when its shell is opened - so the document names the shell,
    /// never this type. Null on a module library.
    /// </summary>
    public Type? Routes { get; init; }
}
