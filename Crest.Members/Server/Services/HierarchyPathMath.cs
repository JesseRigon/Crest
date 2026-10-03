namespace Crest.Members.Services;

/// <summary>
/// Pure materialized-path helpers for the hierarchy table (unit-tested directly).
/// Paths are "/"-delimited node-id chains ENDING in the node's own id with a trailing
/// slash — e.g. the root-level node 12 has path "/12/", its child 45 has "/12/45/".
/// Subtree membership is then a plain prefix match, which is what lets subtree queries
/// push into SQL as LIKE 'path%' (YesSql's query layer cannot express recursion).
/// </summary>
public static class HierarchyPathMath
{
    public const char Separator = '/';

    /// <summary>The path of a node given its parent's path (null/empty for a
    /// root-level node) and its own id.</summary>
    public static string BuildPath(string? parentPath, long id)
        => string.IsNullOrEmpty(parentPath)
            ? $"{Separator}{id}{Separator}"
            : $"{parentPath}{id}{Separator}";

    /// <summary>The ancestor node ids encoded in a path, root first, self LAST.</summary>
    public static IReadOnlyList<long> Chain(string path)
        => path.Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(long.Parse)
            .ToArray();

    /// <summary>Whether <paramref name="path"/> is inside (or is) the subtree rooted at
    /// <paramref name="ancestorPath"/>.</summary>
    public static bool IsWithin(string path, string ancestorPath)
        => path.StartsWith(ancestorPath, StringComparison.Ordinal);

    /// <summary>Rewrites a descendant's path after its subtree root moved from
    /// <paramref name="oldAncestorPath"/> to <paramref name="newAncestorPath"/>.</summary>
    public static string Reroot(string path, string oldAncestorPath, string newAncestorPath)
        => string.Concat(newAncestorPath, path.AsSpan(oldAncestorPath.Length));
}
