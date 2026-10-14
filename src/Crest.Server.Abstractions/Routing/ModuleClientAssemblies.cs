namespace Crest.Routing;

/// <summary>
/// Module client libraries (<c>*.BlazorWasm</c>) and the module each belongs to.
/// </summary>
/// <remarks>
/// A module client library is named for its module ("Crest.Members.Member.BlazorWasm"
/// belongs to "Crest.Members"), the convention the lazy-module generator, the server's
/// route table and the shell contracts all rely on. An assembly belongs to the module with
/// the LONGEST matching id, so a library of "Crest.Members" is never credited to "Crest".
/// </remarks>
public static class ModuleClientAssemblies
{
    public const string Suffix = ".BlazorWasm";

    public static bool IsModuleClient(string? assemblyName) =>
        assemblyName?.EndsWith(Suffix, StringComparison.Ordinal) == true;

    /// <summary>
    /// The module a client assembly belongs to: the longest module id the assembly name
    /// starts with, followed by a dot. Null when no module matches.
    /// </summary>
    public static string? OwningExtensionId(string assemblyName, IEnumerable<string> extensionIds) =>
        extensionIds
            .Where(id => assemblyName.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(id => id.Length)
            .FirstOrDefault();
}
