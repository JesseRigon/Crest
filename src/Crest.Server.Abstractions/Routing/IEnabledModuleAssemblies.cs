using System.Reflection;

namespace Crest.Routing;

/// <summary>
/// The module client libraries whose module is enabled for this tenant: a disabled module is
/// absent - its pages are not in the route table, its endpoints are vetoed and its library is
/// not in the manifest the browser loads from (docs/shells-and-themes.md › A disabled module
/// is absent for that tenant).
/// </summary>
public interface IEnabledModuleAssemblies
{
    /// <summary>The enabled module client libraries, by assembly name.</summary>
    Task<IReadOnlySet<string>> GetEnabledAsync();

    /// <summary>
    /// Whether an assembly's pages may be served for this tenant: true for anything that is
    /// not a module client library (theme clients, the app), and for a module client library
    /// whose module has an enabled feature.
    /// </summary>
    Task<bool> IsEnabledAsync(Assembly assembly);
}
