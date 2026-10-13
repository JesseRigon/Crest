using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Files;

/// <summary>How an add-on works on a file (docs/media.md § 8, ruling 2026-10-06).</summary>
[Flags]
public enum CrestFileAddOnModes
{
    /// <summary>Simple edits right in the viewer pane, without leaving the drive browser.</summary>
    InPlace = 1,

    /// <summary>The add-on's own full editor, opened over the browser.</summary>
    Full = 2,
}

/// <summary>
/// The add-on seam of the viewer pane (docs/media.md § 8): marks a component as an add-on for
/// the file types it lists. Types are MIME types (<c>image/png</c>), MIME families
/// (<c>image/*</c>) or extensions (<c>.xlsx</c>). The component takes a
/// <see cref="CrestFileViewerContext"/> parameter named <c>Context</c>. Add-ons come from any
/// module or third party; they work through the file-object API with the user's own access and
/// save back as a new version or file object, through the same upload pipeline (scanning
/// included). With no add-on for a type, the pane previews.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CrestFileViewerAddOnAttribute(string name, params string[] types) : Attribute
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Types { get; } = types;

    public CrestFileAddOnModes Modes { get; init; } = CrestFileAddOnModes.InPlace | CrestFileAddOnModes.Full;

    /// <summary>Higher wins when several add-ons claim a type.</summary>
    public int Priority { get; init; }

    public bool Matches(string? mimeType, string? extension) =>
        Types.Any(type =>
            type.StartsWith('.') ? string.Equals(type[1..], extension, StringComparison.OrdinalIgnoreCase)
            : type.EndsWith("/*", StringComparison.Ordinal) ? mimeType?.StartsWith(type[..^1], StringComparison.OrdinalIgnoreCase) == true
            : string.Equals(type, mimeType, StringComparison.OrdinalIgnoreCase));
}

/// <summary>What an add-on is handed: the file, how it is shown, and how to save back.</summary>
public sealed class CrestFileViewerContext
{
    public required CrestFileItem File { get; init; }

    public required CrestFilesClient Files { get; init; }

    /// <summary>Where the caller reads the published bytes (signed when the file is private).</summary>
    public string? Url { get; init; }

    public CrestFileAddOnModes Mode { get; init; }

    /// <summary>Whether the caller may save (Editor or above).</summary>
    public bool CanEdit => File.CanEdit;

    /// <summary>Saves new bytes as a new version of this file, through the upload pipeline; the
    /// pane reloads the file when it lands.</summary>
    public Func<Stream, string?, Task<CrestFilesResult<CrestFileItem>>> SaveVersionAsync { get; init; } = (_, _) => Task.FromResult(new CrestFilesResult<CrestFileItem>(null, "Saving is not available here.", 0));

    /// <summary>Closes a full editor.</summary>
    public Func<Task> CloseAsync { get; init; } = () => Task.CompletedTask;
}

/// <summary>A registered add-on: its component and what it declared.</summary>
public sealed record CrestFileViewerAddOn(Type Component, CrestFileViewerAddOnAttribute Declaration);

/// <summary>
/// The add-ons the loaded assemblies declare. Module assemblies can load late (lazy modules), so
/// the scan is per assembly and repeated for assemblies loaded since.
/// </summary>
public static class CrestFileViewerAddOns
{
    private static readonly ConcurrentDictionary<Assembly, CrestFileViewerAddOn[]> Scanned = new();

    public static IReadOnlyList<CrestFileViewerAddOn> All()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || Scanned.ContainsKey(assembly) || !References(assembly))
            {
                continue;
            }

            Scanned[assembly] = Scan(assembly);
        }

        return [.. Scanned.Values.SelectMany(addOns => addOns)];
    }

    /// <summary>The add-ons for a file, best first; only those offering <paramref name="mode"/>.</summary>
    public static IReadOnlyList<CrestFileViewerAddOn> For(CrestFileItem file, CrestFileAddOnModes mode) =>
        [.. All()
            .Where(addOn => addOn.Declaration.Modes.HasFlag(mode) && addOn.Declaration.Matches(file.Type, file.Extension))
            .OrderByDescending(addOn => addOn.Declaration.Priority)
            .ThenBy(addOn => addOn.Declaration.Name, StringComparer.Ordinal)];

    private static bool References(Assembly assembly)
    {
        var self = typeof(CrestFileViewerAddOnAttribute).Assembly;
        return assembly == self || assembly.GetReferencedAssemblies().Any(name => name.Name == self.GetName().Name);
    }

    private static CrestFileViewerAddOn[] Scan(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetExportedTypes();
        }
        catch (Exception)
        {
            return [];
        }

        return [.. types
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => (type, declaration: type.GetCustomAttribute<CrestFileViewerAddOnAttribute>()))
            .Where(pair => pair.declaration is not null)
            .Select(pair => new CrestFileViewerAddOn(pair.type, pair.declaration!))];
    }
}
