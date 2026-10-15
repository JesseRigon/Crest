namespace Grove.Display.Management.Components;

/// <summary>The parameter types of the schema vocabulary (§ 1, modelled on Plasmic's registerComponent).</summary>
public enum ParameterKind
{
    String,
    Number,
    Boolean,
    Choice,
    Object,
    Array,

    /// <summary>A binding to a query or the context (a slot).</summary>
    DataSource,

    /// <summary>An event: the actions bound to it run when it fires (decision 5, § 4a).</summary>
    EventHandler,

    /// <summary>An event that also publishes a value other nodes bind to (§ 4a).</summary>
    Output,

    /// <summary>A module value type from <c>Grove.Data.Types</c>.</summary>
    ValueType,
}

public sealed record ParameterDescriptor(
    string Name,
    Type ClrType,
    ParameterKind Kind,
    string Editor,
    string? DisplayName = null,
    string? Group = null,
    bool Required = false,
    bool Hidden = false,
    bool Advanced = false,
    bool ReadOnly = false,
    IReadOnlyList<string>? Choices = null,
    IReadOnlyList<string>? Accepts = null,
    Type? ValueType = null)
{
    /// <summary>An event or an output: a callback the renderer supplies, never a stored value.</summary>
    public bool IsEvent => Kind is ParameterKind.EventHandler or ParameterKind.Output;
}

public sealed record ZoneDescriptor(string Name, IReadOnlyList<string> Allowed, int Min = 0, int Max = 0)
{
    public bool Admits(string component) => Allowed.Count == 0 || Allowed.Contains(component, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One registry entry (§ 1): a component the tree may name, its schema, and where it may appear.
/// The same descriptor serves the server (validation, defaults), the browser renderer and the
/// designer's property pane. A primitive's <see cref="Type"/> is code; a composite template has
/// none and is resolved through the template binding.
/// </summary>
public sealed record ComponentDescriptor(
    string Name,
    Type? Type,
    IReadOnlyList<ParameterDescriptor> Parameters,
    IReadOnlyList<ZoneDescriptor> Zones,
    IReadOnlySet<ShellKind> Shells,
    string? Module = null,
    string? Feature = null,
    string? Permission = null,
    ComponentInstances Instances = ComponentInstances.Multiple,
    bool IsWrapper = false,
    bool IsHost = false,
    string? DisplayName = null,
    string? Category = null,
    string? Icon = null,
    bool IsOverlay = false)
{
    /// <summary>The value types an editor component edits; empty for anything that is not an editor.</summary>
    public IReadOnlyList<string> Edits { get; init; } = [];

    /// <summary>The outputs other nodes may bind to (§ 4a).</summary>
    public IEnumerable<ParameterDescriptor> Outputs => Parameters.Where(p => p.Kind == ParameterKind.Output);

    public ParameterDescriptor? Parameter(string name) => Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public ZoneDescriptor? Zone(string name) => Zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The shells a component may appear in (from the assembly's shell attribute).</summary>
public enum ShellKind
{
    Admin,
    Site,
    Member,
}
