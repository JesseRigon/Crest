namespace Crest.DisplayManagement.Components;

/// <summary>
/// The open zone keys Crest ships (decision 6): any module may fill them without declaring them.
/// Every other key is declared by the module that owns it (<see cref="ZoneKeyAttribute"/>), under
/// its own prefix (<c>{module}:{page}:{region}</c>).
/// </summary>
public static class ZoneKeys
{
    public const string Header = "Header";
    public const string Navigation = "Navigation";
    public const string Content = "Content";
    public const string Toolbar = "Toolbar";
    public const string Status = "Status";
    public const string Sidebar = "Sidebar";
    public const string Footer = "Footer";

    /// <summary>The well-known keys as registry entries; <see cref="Content"/> is required on a layout.</summary>
    public static IReadOnlyList<ZoneKeyDescriptor> WellKnown { get; } =
    [
        new(Header, Module: null, [], DisplayName: "Header"),
        new(Navigation, Module: null, [], DisplayName: "Navigation"),
        new(Content, Module: null, [], RequiredOnLayout: true, DisplayName: "Content"),
        new(Toolbar, Module: null, [], DisplayName: "Toolbar"),
        new(Status, Module: null, [], DisplayName: "Status"),
        new(Sidebar, Module: null, [], DisplayName: "Sidebar"),
        new(Footer, Module: null, [], DisplayName: "Footer"),
    ];
}

/// <summary>
/// Declares an open zone key a module owns (decision 6): what may fill it, single or multiple
/// instances, whether a layout must have it. A key ending in <c>*</c> declares a family under a
/// prefix (<c>parties:pane:*</c>, one key per party type). Read from the assembly with its
/// component catalog.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ZoneKeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;

    public string? DisplayName { get; set; }

    /// <summary>Component names allowed in the zone; empty allows any.</summary>
    public string[] Allowed { get; set; } = [];

    public ComponentInstances Instances { get; set; } = ComponentInstances.Multiple;

    public bool RequiredOnLayout { get; set; }
}

/// <summary>One zone key in the registry (decision 6): the schema the designer's zone picker, the
/// layer rules and template validation read.</summary>
public sealed record ZoneKeyDescriptor(
    string Key,
    string? Module,
    IReadOnlyList<string> Allowed,
    ComponentInstances Instances = ComponentInstances.Multiple,
    bool RequiredOnLayout = false,
    string? DisplayName = null)
{
    /// <summary>A family of keys under a prefix (the key ends in <c>*</c>).</summary>
    public bool IsFamily => Key.EndsWith('*');

    public bool Matches(string key) =>
        IsFamily
            ? key.StartsWith(Key[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(Key, key, StringComparison.OrdinalIgnoreCase);

    public bool Admits(string component) => Allowed.Count == 0 || Allowed.Contains(component, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A component a module puts into an open zone key in code (decision 6, the page-region seam of
/// docs/page-regions.md folded in): the key, the component and its default priority (lower
/// first). The registry collects them from the module libraries with their components.
/// </summary>
public sealed record ZoneContribution(string Key, Type ComponentType, int Priority, string? Module = null);
