using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The dev-time fallback (§ 1): an assembly's components read by reflection, with the same
/// output the source generator emits. Used only for an assembly without
/// <c>[assembly: ComponentCatalog]</c> (one built without the generator); see
/// <see cref="ComponentCatalogs.ForAssembly"/>.
/// </summary>
[RequiresUnreferencedCode("Reads [Parameter] properties by reflection; a trimmed assembly needs the generated catalog.")]
public sealed class ReflectionComponentCatalog(Assembly assembly) : IComponentCatalog
{
    private const string ShellAttributeName = "Crest.Components.Modules.CrestShellAttribute";

    public IEnumerable<ComponentDescriptor> Describe()
    {
        var shells = ShellsOf(assembly);
        var module = assembly.GetName().Name;

        foreach (var type in TypesOf(assembly))
        {
            if (type.IsAbstract || type.IsGenericTypeDefinition || !typeof(IComponent).IsAssignableFrom(type))
            {
                continue;
            }

            var marker = type.GetCustomAttribute<BlazorComponentAttribute>(inherit: false);
            if (marker is null)
            {
                continue;
            }

            yield return Describe(type, marker, shells, module);
        }
    }

    /// <summary>One component's descriptor; the shells and module are the declaring assembly's.</summary>
    public static ComponentDescriptor Describe(Type type, BlazorComponentAttribute marker, IReadOnlySet<ShellKind> shells, string? module)
    {
        var parameters = new List<ParameterDescriptor>();
        var zones = new List<ZoneDescriptor>();

        foreach (var property in ParameterProperties(type))
        {
            if (ComponentSchema.IsZone(property.PropertyType))
            {
                var zone = property.GetCustomAttribute<ComponentZoneAttribute>();
                zones.Add(new ZoneDescriptor(property.Name, zone?.Allowed ?? [], zone?.Min ?? 0, zone?.Max ?? 0));
                continue;
            }

            var refinement = property.GetCustomAttribute<ComponentParameterAttribute>();
            var (kind, editor, choices) = ComponentSchema.Infer(property.PropertyType);
            if (refinement?.Choices is { } declared)
            {
                (kind, editor, choices) = (ParameterKind.Choice, ComponentSchema.Editors.Enum, declared);
            }

            var output = property.GetCustomAttribute<ComponentOutputAttribute>();
            if (output is not null && kind == ParameterKind.EventHandler)
            {
                (kind, editor) = (ParameterKind.Output, ComponentSchema.Editors.Output);
            }

            parameters.Add(new ParameterDescriptor(
                property.Name,
                property.PropertyType,
                kind,
                refinement?.Editor ?? editor,
                refinement?.DisplayName ?? output?.DisplayName,
                refinement?.Group,
                (refinement?.Required ?? false) || property.IsDefined(typeof(EditorRequiredAttribute)),
                refinement?.Hidden ?? false,
                refinement?.Advanced ?? false,
                refinement?.ReadOnly ?? false,
                choices,
                refinement?.Accepts,
                ComponentSchema.EventValueType(property.PropertyType)));
        }

        return new ComponentDescriptor(
            marker.Name,
            type,
            parameters,
            zones,
            shells,
            Module: module,
            Permission: marker.Permission,
            Instances: marker.Instances,
            IsWrapper: marker.IsWrapper,
            IsHost: marker.IsHost,
            DisplayName: marker.DisplayName,
            Category: marker.Category,
            Icon: marker.Icon,
            IsOverlay: marker.IsOverlay)
        {
            Edits = marker.Edits ?? [],
        };
    }

    /// <summary>The shells an assembly's <c>[assembly: CrestShell]</c> names; every shell without one.</summary>
    public static IReadOnlySet<ShellKind> ShellsOf(Assembly assembly)
    {
        var declared = assembly.GetCustomAttributesData()
            .FirstOrDefault(attribute => attribute.AttributeType.FullName == ShellAttributeName);

        return declared is { ConstructorArguments: [{ Value: string shell }] } && ComponentSchema.ParseShell(shell) is { } kind
            ? new HashSet<ShellKind> { kind }
            : ComponentSchema.AllShells;
    }

    // Base class parameters first, each type's in declaration order; a redeclared name keeps
    // its first position and takes the most derived declaration. CaptureUnmatchedValues
    // parameters are the attribute splat, not part of the schema.
    private static IEnumerable<PropertyInfo> ParameterProperties(Type type)
    {
        var hierarchy = new Stack<Type>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            hierarchy.Push(current);
        }

        var byName = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var level in hierarchy)
        {
            foreach (var property in level.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetCustomAttribute<ParameterAttribute>() is not { CaptureUnmatchedValues: false })
                {
                    continue;
                }

                if (!byName.ContainsKey(property.Name))
                {
                    order.Add(property.Name);
                }

                byName[property.Name] = property;
            }
        }

        return order.Select(name => byName[name]);
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
