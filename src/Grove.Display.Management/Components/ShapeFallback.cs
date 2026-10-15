#nullable enable

using System.Collections.Concurrent;

namespace Grove.Display.Management.Components;

/// <summary>
/// The shapes the display trees of this tenant could not bind to a component (each became an
/// error node), counted per shape type since the tenant started, with the last request path
/// each was seen on. Read by the coverage report (<c>GET api/grove/components/coverage</c>).
/// </summary>
public sealed class DisplayFallbackLog
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string shapeType, string? path)
        => _entries.AddOrUpdate(
            shapeType,
            _ => new Entry(1, path),
            (_, entry) => new Entry(entry.Count + 1, path ?? entry.LastPath));

    public IReadOnlyDictionary<string, Entry> Entries => _entries;

    public sealed record Entry(long Count, string? LastPath);
}
