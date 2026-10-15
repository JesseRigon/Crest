using System.Text.Json.Nodes;
using Grove.Data.Documents;

namespace Grove.Display.Templates.Models;

/// <summary>
/// The tenant's overrides (docs/blazor-display.md § 5a): saved variants of components and
/// adjustments of single placements. Literal values only: a value derived from data (a binding, a
/// slot) is never overridden.
/// </summary>
public sealed class ComponentOverridesDocument : Document
{
    public List<ComponentVariant> Variants { get; set; } = [];

    public List<InstanceOverride> Instances { get; set; } = [];
}

/// <summary>
/// A node override: a component saved with values under its own name and placed by that name like
/// a component (a "blue title"). Its base is a component, a composite or another variant; it
/// inherits every value it does not set, so a change to the base reaches it unless it sets that value.
/// </summary>
public sealed class ComponentVariant
{
    public string Name { get; set; } = string.Empty;

    public string Base { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Category { get; set; }

    public string? Icon { get; set; }

    public JsonObject Values { get; set; } = [];
}

/// <summary>An instance override: values for one placement (a node's instance id), over everything it inherits.</summary>
public sealed class InstanceOverride
{
    public string InstanceId { get; set; } = string.Empty;

    public JsonObject Values { get; set; } = [];
}
