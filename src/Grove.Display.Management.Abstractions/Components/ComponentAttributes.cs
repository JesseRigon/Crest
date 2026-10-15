using System.Diagnostics.CodeAnalysis;

namespace Grove.Display.Management.Components;

/// <summary>
/// Declares a Razor component in the component registry (docs/blazor-display.md § 1): the name
/// a shape binds it by, how the designer's palette shows it, and the rules the tree builder and
/// the designer enforce. The component's <c>[Parameter]</c> properties are its schema;
/// <see cref="ComponentParameterAttribute"/> and <see cref="ComponentZoneAttribute"/> refine them.
/// The source generator turns these into the assembly's <see cref="IComponentCatalog"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BlazorComponentAttribute(string name) : Attribute
{
    /// <summary>The shape type it binds by default ("Content", "Heading", "Widget__Image"); alternates are names too.</summary>
    public string Name { get; } = name;

    public string? DisplayName { get; set; }

    public string? Category { get; set; }

    public string? Icon { get; set; }

    /// <summary>A permission name: the node is left out of the tree for a viewer who lacks it.</summary>
    public string? Permission { get; set; }

    /// <summary>Several instances on one page break some components' logic (a page header).</summary>
    public ComponentInstances Instances { get; set; } = ComponentInstances.Multiple;

    /// <summary>A wrapper that takes the one child it wraps (Plasmic's <c>isAttachment</c>).</summary>
    public bool IsWrapper { get; set; }

    /// <summary>A host declares open zones other features contribute to.</summary>
    public bool IsHost { get; set; }

    /// <summary>An overlay (a modal, a drawer, a context menu, a popover) renders only while a
    /// client action has it open (§ 4a); it reads its position and close callback from the
    /// cascaded <see cref="DisplayOverlay"/>.</summary>
    public bool IsOverlay { get; set; }

    /// <summary>
    /// The value types this component edits (an editor, § 1 and decision 2): content field and
    /// part names (<c>TextField</c>, <c>TitlePart</c>) or value types; <c>*</c> edits anything.
    /// The editor tree picks a field's editor by it.
    /// </summary>
    public string[]? Edits { get; set; }
}

public enum ComponentInstances
{
    Multiple,
    Single,
}

/// <summary>Refines how a <c>[Parameter]</c> is presented and validated (§ 1, the schema vocabulary).</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ComponentParameterAttribute : Attribute
{
    /// <summary>The editor kind (text, number, bool, enum, picker, color, token, markup, ...); inferred from the type when absent.</summary>
    public string? Editor { get; set; }

    public string? DisplayName { get; set; }

    public string? Group { get; set; }

    public bool Required { get; set; }

    public bool Hidden { get; set; }

    public bool Advanced { get; set; }

    public bool ReadOnly { get; set; }

    /// <summary>The value types a picker or a slot binding accepts (a data source's declared types).</summary>
    public string[]? Accepts { get; set; }

    /// <summary>Static options: makes the parameter a <see cref="ParameterKind.Choice"/> (an enum's names are inferred).</summary>
    public string[]? Choices { get; set; }
}

/// <summary>
/// Declares an <c>EventCallback&lt;T&gt;</c> parameter as an output (§ 4a): a value the component
/// publishes (a list's selection, a form's value). The renderer keeps the latest value in the
/// tree's <see cref="DisplayState"/> under the node's instance id and the parameter's name, where
/// other nodes' bindings read it, then runs the actions bound to it like any event.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ComponentOutputAttribute : Attribute
{
    public string? DisplayName { get; set; }
}

/// <summary>Declares a <c>RenderFragment</c> parameter as a zone: what may fill it and how many.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ComponentZoneAttribute : Attribute
{
    /// <summary>Component names allowed in the zone; empty allows any.</summary>
    public string[] Allowed { get; set; } = [];

    public int Min { get; set; }

    /// <summary>0 for no maximum.</summary>
    public int Max { get; set; }
}

/// <summary>Names the generated catalog of an assembly's components, so scanners find it from metadata.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ComponentCatalogAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type catalogType) : Attribute
{
    /// <summary>Kept with its constructor through trimming: the scanners create it by reflection.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type CatalogType { get; } = catalogType;
}
