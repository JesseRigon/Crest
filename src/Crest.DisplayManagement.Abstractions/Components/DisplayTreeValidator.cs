using System.Text.Json.Nodes;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// Checks a tree's interaction wiring against the registry (§ 4a › Validation): a binding names
/// a node of the tree and one of its outputs, or a page variable; an event names an event or
/// output parameter; an open, close or toggle targets an overlay node; a workflow action names a
/// workflow; a predicate uses a known operator over binding sources (docs/widgets.md); bindings
/// form no cycle; a component declared single per page (decision 6) appears once. Used on save (templates, pages) and by the designer. The
/// errors are paths into the tree with the reason; an empty list is a valid tree.
/// </summary>
public static class DisplayTreeValidator
{
    public static IReadOnlyList<DisplayDiagnostic> Validate(DisplayNode root, IComponentRegistry registry)
    {
        var errors = new List<DisplayDiagnostic>();
        var nodes = new Dictionary<string, DisplayNode>(StringComparer.Ordinal);
        foreach (var node in Walk(root))
        {
            if (!nodes.TryAdd(node.Id, node))
            {
                errors.Add(new(node.Id, "duplicate", $"The instance id '{node.Id}' appears more than once in the tree."));
            }
        }

        var singles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes.Values)
        {
            if (registry.TryGet(node.Component, out var single) && single.Instances == ComponentInstances.Single && !singles.Add(single.Name))
            {
                errors.Add(new(node.Id, "instances", $"'{single.Name}' may appear only once on a page."));
            }
        }

        foreach (var node in nodes.Values)
        {
            if (!registry.TryGet(node.Component, out var descriptor))
            {
                if (node.Component is not (DisplayNode.MarkupComponent or DisplayNode.ErrorComponent or DisplayNode.ShapeZoneComponent))
                {
                    errors.Add(new(node.Id, "unknown", $"'{node.Component}' is not a registered component."));
                }

                continue;
            }

            foreach (var (name, binding) in node.Bindings ?? [])
            {
                var path = $"{node.Id}.{name}";
                if (descriptor.Parameter(name) is not { IsEvent: false } parameter)
                {
                    errors.Add(new(path, "binding", $"'{name}' is not a value parameter of '{descriptor.Name}'."));
                    continue;
                }

                CheckBinding(binding, path, nodes, registry, errors, parameter, inEvent: false);
            }

            foreach (var (name, actions) in node.Events ?? [])
            {
                var path = $"{node.Id}.{name}";
                if (descriptor.Parameter(name) is not { IsEvent: true })
                {
                    errors.Add(new(path, "event", $"'{name}' is not an event or output of '{descriptor.Name}'."));
                    continue;
                }

                for (var i = 0; i < actions.Count; i++)
                {
                    CheckAction(actions[i], $"{path}[{i}]", nodes, registry, errors);
                }
            }
        }

        if (FindCycle(nodes) is { } cycle)
        {
            errors.Add(new(cycle[0], "cycle", $"The bindings form a cycle: {string.Join(" → ", cycle)}."));
        }

        return errors;
    }

    /// <summary>
    /// The wiring check plus the schema check of every node's stored properties (a tree authored
    /// as data, docs/widgets.md: an item's <c>DisplayTreePart</c>): each property is a value
    /// parameter of the component and converts to its type; a required parameter has a value, a
    /// binding or a slot. Nothing here can be code: the tree names registered components only.
    /// </summary>
    public static IReadOnlyList<DisplayDiagnostic> ValidateStored(DisplayNode root, IComponentRegistry registry)
    {
        var errors = new List<DisplayDiagnostic>(Validate(root, registry));
        foreach (var node in Walk(root))
        {
            if (!registry.TryGet(node.Component, out var descriptor))
            {
                continue;
            }

            foreach (var (name, value) in node.Properties)
            {
                var path = $"{node.Id}.{name}";
                if (descriptor.Parameter(name) is not { } parameter)
                {
                    errors.Add(new(path, "property", $"'{name}' is not a parameter of '{descriptor.Name}'."));
                }
                else if (parameter.IsEvent)
                {
                    errors.Add(new(path, "property", $"'{parameter.Name}' is an event: bind its actions under events."));
                }
                else if (parameter.Choices is { Count: > 0 } choices && value is JsonValue text && text.TryGetValue<string>(out var choice) && !choices.Contains(choice, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(new(path, "property", $"'{parameter.Name}' must be one of {string.Join(", ", choices)}."));
                }
                else if (!ComponentParameters.TryConvert(value, parameter.ClrType, out _, out var error))
                {
                    errors.Add(new(path, "property", $"'{parameter.Name}' is not a valid {parameter.ClrType.Name}: {error}"));
                }
            }

            foreach (var parameter in descriptor.Parameters.Where(parameter => parameter.Required && !parameter.IsEvent))
            {
                if (!node.Properties.ContainsKey(parameter.Name) && node.Bindings?.ContainsKey(parameter.Name) != true && node.Slots?.ContainsKey(parameter.Name) != true)
                {
                    errors.Add(new($"{node.Id}.{parameter.Name}", "property", $"The required parameter '{parameter.Name}' of '{descriptor.Name}' has no value."));
                }
            }

            foreach (var zone in node.Zones.Keys.Where(zone => descriptor.Zone(zone) is null))
            {
                errors.Add(new($"{node.Id}.{zone}", "zone", $"'{descriptor.Name}' has no zone '{zone}'."));
            }
        }

        return errors;
    }

    /// <summary>Every node of the tree, depth first.</summary>
    public static IEnumerable<DisplayNode> Walk(DisplayNode root)
    {
        var stack = new Stack<DisplayNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.Zones.Values.SelectMany(children => children).Reverse())
            {
                stack.Push(child);
            }
        }
    }

    private static void CheckAction(DisplayAction action, string path, Dictionary<string, DisplayNode> nodes, IComponentRegistry registry, List<DisplayDiagnostic> errors)
    {
        switch (action.Kind)
        {
            case DisplayActionKind.Set when string.IsNullOrWhiteSpace(action.Target):
                errors.Add(new(path, "action", "A set action needs the variable it sets."));
                break;
            case DisplayActionKind.Open or DisplayActionKind.Toggle when string.IsNullOrWhiteSpace(action.Target):
                errors.Add(new(path, "action", $"An {action.Kind.ToString().ToLowerInvariant()} action needs the overlay it acts on."));
                break;
            case DisplayActionKind.Open or DisplayActionKind.Close or DisplayActionKind.Toggle when action.Target is { Length: > 0 } target:
                if (!nodes.TryGetValue(target, out var overlay))
                {
                    errors.Add(new(path, "action", $"The overlay '{target}' is not in the tree."));
                }
                else if (!registry.TryGet(overlay.Component, out var descriptor) || !descriptor.IsOverlay)
                {
                    errors.Add(new(path, "action", $"'{target}' ({overlay.Component}) is not an overlay component."));
                }

                break;
            case DisplayActionKind.Workflow when string.IsNullOrWhiteSpace(action.Workflow):
                errors.Add(new(path, "action", "A workflow action needs the workflow it runs."));
                break;
            case DisplayActionKind.Navigate when action.Value is null:
                errors.Add(new(path, "action", "A navigate action needs the URL it goes to."));
                break;
        }

        if (action.Value is { } value)
        {
            CheckBinding(value, path + ".value", nodes, registry, errors, parameter: null, inEvent: true);
        }

        foreach (var (name, argument) in action.Arguments ?? [])
        {
            CheckBinding(argument, $"{path}.{name}", nodes, registry, errors, parameter: null, inEvent: true);
        }
    }

    private static void CheckBinding(
        DisplayBinding binding,
        string path,
        Dictionary<string, DisplayNode> nodes,
        IComponentRegistry registry,
        List<DisplayDiagnostic> errors,
        ParameterDescriptor? parameter,
        bool inEvent)
    {
        if (binding.Event && !inEvent)
        {
            errors.Add(new(path, "binding", "The event payload is only available to an event's actions."));
            return;
        }

        if (binding.Combinator is { } combinator)
        {
            if (!DisplayPredicates.Combinators.Contains(combinator, StringComparer.Ordinal))
            {
                errors.Add(new(path, "binding", $"'{combinator}' is not one of {string.Join(", ", DisplayPredicates.Combinators)}."));
                return;
            }

            var terms = binding.Terms ?? [];
            if (terms.Count == 0 || (combinator == DisplayPredicates.Not && terms.Count != 1))
            {
                errors.Add(new(path, "binding", combinator == DisplayPredicates.Not ? "'not' takes one term." : $"'{combinator}' needs its terms."));
                return;
            }

            for (var i = 0; i < terms.Count; i++)
            {
                CheckBinding(terms[i], $"{path}[{i}]", nodes, registry, errors, parameter: null, inEvent);
            }

            return;
        }

        if (binding.Operator is { } @operator)
        {
            if (!DisplayPredicates.Operators.Contains(@operator, StringComparer.Ordinal))
            {
                errors.Add(new(path, "binding", $"'{@operator}' is not one of {string.Join(", ", DisplayPredicates.Operators)}."));
                return;
            }

            if (binding.Node is null && binding.Variable is null && !binding.Event)
            {
                errors.Add(new(path, "binding", "A comparison compares a binding source (a node's output, a page variable, the event)."));
                return;
            }

            if (DisplayPredicates.TakesOperand(@operator) && binding.Operand is null)
            {
                errors.Add(new(path, "binding", $"'{@operator}' needs a right side."));
                return;
            }

            if (binding.Operand is { } operand)
            {
                CheckBinding(operand, path + ".operand", nodes, registry, errors, parameter: null, inEvent);
            }

            // Then the left side, as any binding.
            parameter = null;
        }

        if (binding.Node is not { } nodeId)
        {
            return;
        }

        if (!nodes.TryGetValue(nodeId, out var source))
        {
            errors.Add(new(path, "binding", $"The node '{nodeId}' is not in the tree."));
            return;
        }

        // A composite re-publishes inner outputs under its own id.
        if (binding.Output is { } aliased && source.OutputAliases?.ContainsKey(aliased) == true)
        {
            return;
        }

        if (!registry.TryGet(source.Component, out var descriptor))
        {
            return;
        }

        if (binding.Output is not { } outputName || descriptor.Parameter(outputName) is not { Kind: ParameterKind.Output } output)
        {
            errors.Add(new(path, "binding", $"'{binding.Output}' is not an output of '{descriptor.Name}'."));
            return;
        }

        // A whole value (no path) must fit the parameter; a path reads into it, checked at run time.
        if (parameter is not null && string.IsNullOrEmpty(binding.Path) && output.ValueType is { } produced && !Fits(produced, parameter.ClrType))
        {
            errors.Add(new(path, "binding", $"'{descriptor.Name}.{output.Name}' produces {produced.Name}, which '{parameter.Name}' ({parameter.ClrType.Name}) does not take."));
        }
    }

    private static bool Fits(Type produced, Type wanted)
    {
        wanted = Nullable.GetUnderlyingType(wanted) ?? wanted;
        produced = Nullable.GetUnderlyingType(produced) ?? produced;
        return wanted == typeof(object) || wanted.IsAssignableFrom(produced) || (wanted == typeof(string) && !produced.IsClass);
    }

    // A node's bound parameters depend on the nodes whose outputs they read.
    private static List<string>? FindCycle(Dictionary<string, DisplayNode> nodes)
    {
        var edges = nodes.Values.ToDictionary(
            node => node.Id,
            node => (node.Bindings?.Values ?? Enumerable.Empty<DisplayBinding>()).SelectMany(DisplayPredicates.Sources).Where(nodes.ContainsKey).Distinct().ToList(),
            StringComparer.Ordinal);

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();

        List<string>? Visit(string id)
        {
            state[id] = 1;
            stack.Add(id);
            foreach (var next in edges[id])
            {
                if (state.GetValueOrDefault(next) == 1)
                {
                    return [.. stack[stack.IndexOf(next)..], next];
                }

                if (state.GetValueOrDefault(next) == 0 && Visit(next) is { } found)
                {
                    return found;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[id] = 2;
            return null;
        }

        foreach (var id in edges.Keys)
        {
            if (state.GetValueOrDefault(id) == 0 && Visit(id) is { } cycle)
            {
                return cycle;
            }
        }

        return null;
    }
}
