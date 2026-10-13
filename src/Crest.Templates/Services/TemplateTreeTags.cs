using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Implementation;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Templates.Services;

/// <summary>
/// The parser of tree templates (docs/blazor-display.md § 5): Fluid with the platform's filters
/// and member access (the shared <c>TemplateOptions</c>), but tags that emit display-tree nodes
/// instead of HTML. The HTML-rendering platform tags (<c>render_section</c>, <c>layout</c>, the
/// HTML <c>shape</c> and <c>zone</c>) are not here: in a template, structure is nodes.
/// <code>
/// {% compose "Card", id: "summary" %}
///   {% zone "ChildContent" %}{% component "CrestHeading", text: Model.ContentItem.DisplayText %}{% endzone %}
///   {% children "Content" %}
/// {% endcompose %}
/// {% compose "ItemList", id: "invoices" %}{% slot "items", "RecentInvoices" %}{% endcompose %}
/// {% compose "PropertyList" %}{% slot "value", "Invoice", id: nodes.invoices.selected_item.id %}{% endcompose %}
/// </code>
/// </summary>
public sealed class TemplateTreeParser : FluidParser
{
    public TemplateTreeParser(IOptions<FluidParserOptions> options)
        : base(options.Value)
    {
        RegisterParserTag("component", ArgumentsList, TemplateTreeTags.ComponentAsync);
        RegisterParserBlock("compose", ArgumentsList, TemplateTreeTags.ComposeAsync);
        RegisterParserBlock("zone", ArgumentsList, TemplateTreeTags.ZoneAsync);
        RegisterParserTag("children", ArgumentsList, TemplateTreeTags.ChildrenAsync);
        RegisterParserTag("slot", ArgumentsList, TemplateTreeTags.SlotAsync);
        RegisterParserTag("shape", ArgumentsList, TemplateTreeTags.ShapeAsync);
        RegisterParserBlock("on", ArgumentsList, TemplateTreeTags.OnAsync);
        RegisterParserTag("set_variable", ArgumentsList, TemplateTreeTags.SetVariableAsync);
        RegisterParserTag("open", ArgumentsList, (arguments, writer, encoder, context) => TemplateTreeTags.OverlayAsync(DisplayActionKind.Open, arguments, context));
        RegisterParserTag("close", ArgumentsList, (arguments, writer, encoder, context) => TemplateTreeTags.OverlayAsync(DisplayActionKind.Close, arguments, context));
        RegisterParserTag("toggle", ArgumentsList, (arguments, writer, encoder, context) => TemplateTreeTags.OverlayAsync(DisplayActionKind.Toggle, arguments, context));
        RegisterParserTag("run_workflow", ArgumentsList, TemplateTreeTags.RunWorkflowAsync);
        RegisterParserTag("navigate", ArgumentsList, TemplateTreeTags.NavigateAsync);
        RegisterParserTag("submit", ArgumentsList, TemplateTreeTags.SubmitAsync);
    }
}

/// <summary>The tags of <see cref="TemplateTreeParser"/>.</summary>
public static class TemplateTreeTags
{
    /// <summary><c>{% component "Name", id: "x", parameter: value %}</c>: a leaf node.</summary>
    public static async ValueTask<Completion> ComponentAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        if (await CreateAsync(arguments, context, state) is { } created)
        {
            state.Append(created.Node);
        }

        return Completion.Normal;
    }

    /// <summary><c>{% compose "Name", ... %} children, zones, events {% endcompose %}</c>.</summary>
    public static async ValueTask<Completion> ComposeAsync(IReadOnlyList<FilterArgument> arguments, IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        if (await CreateAsync(arguments, context, state) is not { Descriptor: { } descriptor } created)
        {
            return Completion.Normal;
        }

        state.Push(new TemplateTreeState.Frame(created.Node, descriptor));
        try
        {
            var completion = await RunAsync(statements, writer, encoder, context);
            if (completion != Completion.Normal)
            {
                return completion;
            }
        }
        finally
        {
            state.Pop();
        }

        state.Append(created.Node);
        return Completion.Normal;
    }

    /// <summary><c>{% zone "Name" %} ... {% endzone %}</c> inside a <c>compose</c>: where its children go.</summary>
    public static async ValueTask<Completion> ZoneAsync(IReadOnlyList<FilterArgument> arguments, IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var name = (await PositionalAsync(arguments, context))?.ToStringValue();
        var frame = state.Current;
        if (frame.Node is null || frame.Actions is not null || string.IsNullOrEmpty(name))
        {
            state.Diagnose(frame.Node?.Id ?? state.Prefix, "dropped", "A zone block belongs directly inside a compose block and needs a name.");
            return Completion.Normal;
        }

        state.FlushText();
        var previous = frame.Zone;
        frame.Zone = name;
        try
        {
            return await RunAsync(statements, writer, encoder, context);
        }
        finally
        {
            state.FlushText();
            frame.Zone = previous;
        }
    }

    /// <summary><c>{% children "Content" %}</c>: the bound shape's own zone, mapped by the tree builder in the same build.</summary>
    public static async ValueTask<Completion> ChildrenAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var zone = (await PositionalAsync(arguments, context))?.ToStringValue();
        state.Append(new DisplayNode
        {
            Id = state.NextId(),
            Component = DisplayNode.ShapeZoneComponent,
            Properties = new(StringComparer.OrdinalIgnoreCase) { [DisplayNode.ShapeZoneProperty] = string.IsNullOrEmpty(zone) ? "ChildContent" : zone },
        });

        return Completion.Normal;
    }

    /// <summary>
    /// <c>{% slot "items", "RecentInvoices", customer: nodes.customers.selected_item.id, path: "items" %}</c>
    /// inside a <c>compose</c>: the parameter fed by a query (decision 4), its parameters literals or
    /// bindings. A slot whose parameters are all literals is read now, as the viewer; a bound one
    /// is re-run by the browser when its parameters change. (A tag, because tag arguments take
    /// plain expressions only: no filter chain, no function call.)
    /// </summary>
    public static async ValueTask<Completion> SlotAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var positional = new List<string>();
        string? path = null;
        var parameters = new Dictionary<string, DisplayBinding>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            var value = await argument.Expression.EvaluateAsync(context);
            if (string.IsNullOrEmpty(argument.Name))
            {
                positional.Add(value.ToStringValue());
            }
            else if (argument.Name == "path")
            {
                path = value.ToStringValue();
            }
            else
            {
                parameters[argument.Name] = TemplateTreeState.ToBinding(value);
            }
        }

        var frame = state.Current;
        if (frame is not { Node: { } node, Descriptor: { } descriptor, Actions: null } || positional.Count < 2)
        {
            state.Diagnose(frame.Node?.Id ?? state.Prefix, "dropped", "A slot belongs directly inside a compose block and names the parameter and the query.");
            return Completion.Normal;
        }

        if ((descriptor.Parameter(ToPascal(positional[0])) ?? descriptor.Parameter(positional[0])) is not { IsEvent: false } parameter)
        {
            state.Diagnose(node.Id, "dropped", $"'{positional[0]}' is not a value parameter of '{descriptor.Name}'.");
            return Completion.Normal;
        }

        var slot = new DisplaySlot { Query = positional[1], Parameters = parameters.Count > 0 ? parameters : null, Path = path };
        (node.Slots ??= new(StringComparer.OrdinalIgnoreCase))[parameter.Name] = slot;
        if (slot.IsStatic && await ResolveSlotAsync(slot, state) is { } resolved)
        {
            node.Properties[parameter.Name] = resolved;
        }

        return Completion.Normal;
    }

    /// <summary>
    /// <c>{% shape "Menu", alias: "alias:main-menu" %}</c>: a platform shape, built by its shape
    /// table (its processing and displaying events, its drivers) and mapped into this tree, so a
    /// template places module-built UI without rendering it.
    /// </summary>
    public static async ValueTask<Completion> ShapeAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        string? type = null;
        var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
        {
            var value = await argument.Expression.EvaluateAsync(context);
            if (string.IsNullOrEmpty(argument.Name) || argument.Name == "type")
            {
                type ??= value.ToStringValue();
            }
            else
            {
                properties[ToPascal(argument.Name)] = value.ToObjectValue();
            }
        }

        if (string.IsNullOrEmpty(type))
        {
            state.Diagnose(state.Prefix, "dropped", "A shape tag needs the shape type.");
            return Completion.Normal;
        }

        var factory = state.Services.GetRequiredService<IShapeFactory>();
        var shape = await factory.CreateAsync(type, Arguments.From(properties));
        var tree = await state.Services.GetRequiredService<IDisplayTreeBuilder>().BuildAsync(shape, cancellationToken: state.Budget.CancellationToken);
        state.Append(tree.Root, checkZone: !string.Equals(tree.Root.Component, DisplayNode.MarkupComponent, StringComparison.Ordinal));
        return Completion.Normal;
    }

    /// <summary><c>{% on "SelectedItem" %} actions {% endon %}</c> inside a <c>compose</c>: the actions an event or output runs.</summary>
    public static async ValueTask<Completion> OnAsync(IReadOnlyList<FilterArgument> arguments, IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var name = (await PositionalAsync(arguments, context))?.ToStringValue();
        var frame = state.Current;
        if (frame is not { Node: { } node, Descriptor: { } descriptor, Actions: null })
        {
            state.Diagnose(state.Prefix, "dropped", "An 'on' block belongs directly inside a compose block.");
            return Completion.Normal;
        }

        if (string.IsNullOrEmpty(name) || descriptor.Parameter(ToPascal(name)) is not { IsEvent: true } parameter)
        {
            state.Diagnose(node.Id, "dropped", $"'{name}' is not an event or output of '{descriptor.Name}'.");
            return Completion.Normal;
        }

        var actions = new List<DisplayAction>();
        state.Push(new TemplateTreeState.Frame(node, descriptor) { Actions = actions });
        context.EnterChildScope();
        try
        {
            context.SetValue("event", new BindingValue(DisplayBinding.FromEvent()));
            var completion = await RunAsync(statements, writer, encoder, context);
            if (completion != Completion.Normal)
            {
                return completion;
            }
        }
        finally
        {
            context.ReleaseScope();
            state.Pop();
        }

        node.Events ??= new(StringComparer.OrdinalIgnoreCase);
        if (!node.Events.TryGetValue(parameter.Name, out var list))
        {
            node.Events[parameter.Name] = list = [];
        }

        list.AddRange(actions);
        return Completion.Normal;
    }

    /// <summary><c>{% set_variable "row", event.item %}</c>.</summary>
    public static async ValueTask<Completion> SetVariableAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var values = await PositionalListAsync(arguments, context);
        if (values.Count < 1 || string.IsNullOrEmpty(values[0].ToStringValue()))
        {
            state.Diagnose(state.Prefix, "dropped", "set_variable needs the variable's name.");
            return Completion.Normal;
        }

        AddAction(state, new DisplayAction
        {
            Kind = DisplayActionKind.Set,
            Target = values[0].ToStringValue(),
            Value = values.Count > 1 ? TemplateTreeState.ToBinding(values[1]) : null,
        });

        return Completion.Normal;
    }

    /// <summary><c>{% open "rowMenu" %}</c>, <c>{% close %}</c>, <c>{% toggle "details" %}</c>.</summary>
    public static async ValueTask<Completion> OverlayAsync(DisplayActionKind kind, IReadOnlyList<FilterArgument> arguments, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var target = (await PositionalAsync(arguments, context))?.ToStringValue();
        AddAction(state, new DisplayAction
        {
            Kind = kind,
            Target = string.IsNullOrEmpty(target) ? null : state.Scope(target),
        });

        return Completion.Normal;
    }

    /// <summary><c>{% run_workflow "post-invoice", into: "result", invoice: event.item.id %}</c>.</summary>
    public static async ValueTask<Completion> RunWorkflowAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        string? workflow = null;
        string? into = null;
        var input = new Dictionary<string, DisplayBinding>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            var value = await argument.Expression.EvaluateAsync(context);
            switch (argument.Name)
            {
                case null or "" or "workflow":
                    workflow ??= value.ToStringValue();
                    break;
                case "into":
                    into = value.ToStringValue();
                    break;
                default:
                    input[argument.Name] = TemplateTreeState.ToBinding(value);
                    break;
            }
        }

        AddAction(state, new DisplayAction
        {
            Kind = DisplayActionKind.Workflow,
            Workflow = workflow,
            Target = into,
            Arguments = input.Count > 0 ? input : null,
        });

        return Completion.Normal;
    }

    /// <summary><c>{% navigate "/invoices/" | append: event.item.id %}</c>.</summary>
    public static async ValueTask<Completion> NavigateAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var state = TemplateTreeState.From(context);
        var url = await PositionalAsync(arguments, context);
        AddAction(state, new DisplayAction
        {
            Kind = DisplayActionKind.Navigate,
            Value = url is null ? null : TemplateTreeState.ToBinding(url),
        });

        return Completion.Normal;
    }

    /// <summary><c>{% submit %}</c>: submits the current item.</summary>
    public static ValueTask<Completion> SubmitAsync(IReadOnlyList<FilterArgument> arguments, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        AddAction(TemplateTreeState.From(context), new DisplayAction { Kind = DisplayActionKind.Submit });
        return new(Completion.Normal);
    }

    private static void AddAction(TemplateTreeState state, DisplayAction action)
    {
        if (state.Current.Actions is { } actions)
        {
            actions.Add(action);
        }
        else
        {
            state.Diagnose(state.Prefix, "dropped", $"The {action.Kind.ToString().ToLowerInvariant()} action belongs inside an 'on' block.");
        }
    }

    internal sealed record Created(DisplayNode Node, ComponentDescriptor? Descriptor);

    // A registered component becomes a node with its arguments checked against the schema; a
    // published composite template of that name is evaluated (parsed on its own, with only its
    // arguments in scope) and its tree linked in; anything else is diagnosed and left out.
    private static async Task<Created?> CreateAsync(IReadOnlyList<FilterArgument> arguments, TemplateContext context, TemplateTreeState state)
    {
        state.Budget.Check();
        string? name = null;
        string? explicitId = null;
        var named = new List<(string Name, FluidValue Value)>();
        foreach (var argument in arguments)
        {
            var value = await argument.Expression.EvaluateAsync(context);
            if (string.IsNullOrEmpty(argument.Name))
            {
                name ??= value.ToStringValue();
            }
            else if (argument.Name == "id")
            {
                explicitId = value.ToStringValue();
            }
            else
            {
                named.Add((argument.Name, value));
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            state.Diagnose(state.Prefix, "dropped", "A component tag needs the component's name.");
            return null;
        }

        var id = string.IsNullOrEmpty(explicitId) ? state.NextId() : state.Scope(explicitId);

        // A variant (§ 5a) places what it resolves to, with the values it inherits; the tag's own
        // arguments come after them, so they win.
        if (!state.Registry.TryGet(name, out _)
            && state.Services.GetService<ComponentOverrides>() is { } overrides
            && await overrides.ResolveVariantAsync(name) is { } variant)
        {
            name = variant.Component;
            named.InsertRange(0, variant.Values.Select(pair => (pair.Key, ToFluid(pair.Value))));
        }

        if (!state.Registry.TryGet(name, out var descriptor))
        {
            if (await state.Evaluator.EvaluateCompositeAsync(name, id, named, state) is { } composite)
            {
                return new Created(composite, null);
            }

            state.Diagnose(id, "dropped", $"'{name}' is neither a registered component nor a published template.");
            return null;
        }

        var node = new DisplayNode { Id = id, Component = descriptor.Name, Module = descriptor.Module };
        foreach (var (argumentName, value) in named)
        {
            var parameter = descriptor.Parameter(ToPascal(argumentName)) ?? descriptor.Parameter(argumentName);
            if (parameter is null)
            {
                state.Diagnose($"{id}.{argumentName}", "dropped", $"'{argumentName}' is not a parameter of '{descriptor.Name}'.");
                continue;
            }

            if (parameter.IsEvent)
            {
                state.Diagnose($"{id}.{parameter.Name}", "dropped", $"'{parameter.Name}' is an event: bind its actions in an 'on' block.");
                continue;
            }

            switch (value)
            {
                case BindingValue binding:
                    (node.Bindings ??= new(StringComparer.OrdinalIgnoreCase))[parameter.Name] = binding.Binding;
                    break;

                default:
                    if (!ComponentParameters.TryConvert(TemplateTreeState.ToJson(value), parameter.ClrType, out var converted, out var error))
                    {
                        state.Diagnose($"{id}.{parameter.Name}", "dropped", $"'{parameter.Name}' is not a valid {parameter.ClrType.Name}: {error}");
                        continue;
                    }

                    node.Properties[parameter.Name] = DisplayState.ToJson(converted);
                    break;
            }
        }

        foreach (var parameter in descriptor.Parameters.Where(parameter => parameter.Required && !parameter.IsEvent))
        {
            if (!node.Properties.ContainsKey(parameter.Name) && node.Bindings?.ContainsKey(parameter.Name) != true && node.Slots?.ContainsKey(parameter.Name) != true)
            {
                state.Diagnose($"{id}.{parameter.Name}", "missing", $"The required parameter '{parameter.Name}' of '{descriptor.Name}' has no value.");
            }
        }

        return new Created(node, descriptor);
    }

    private static FluidValue ToFluid(JsonNode? value) => value switch
    {
        null => NilValue.Instance,
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => StringValue.Create(text),
        JsonValue scalar when scalar.TryGetValue<bool>(out var flag) => BooleanValue.Create(flag),
        JsonValue scalar when scalar.TryGetValue<decimal>(out var number) => NumberValue.Create(number),
        _ => new ObjectValue(value.DeepClone()),
    };

    // A slot whose parameters are all literals is read while the tree is built, as the viewer.
    private static async Task<JsonNode?> ResolveSlotAsync(DisplaySlot slot, TemplateTreeState state)
    {
        if (state.Services.GetService<IDisplayDataSource>() is not { } source)
        {
            return null;
        }

        var parameters = (slot.Parameters ?? []).ToDictionary(pair => pair.Key, pair => pair.Value.Value?.DeepClone(), StringComparer.Ordinal);
        var result = await source.QueryAsync(slot.Query, parameters, state.Budget.CancellationToken);
        return slot.Path is { Length: > 0 } path ? DisplayState.Navigate(result, path) : result;
    }

    private static async ValueTask<Completion> RunAsync(IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        foreach (var statement in statements)
        {
            context.IncrementSteps();
            var completion = await statement.WriteToAsync(writer, encoder, context);
            if (completion != Completion.Normal)
            {
                return completion;
            }
        }

        return Completion.Normal;
    }

    private static async ValueTask<FluidValue?> PositionalAsync(IReadOnlyList<FilterArgument> arguments, TemplateContext context)
    {
        foreach (var argument in arguments)
        {
            if (string.IsNullOrEmpty(argument.Name) || argument.Name == "name")
            {
                return await argument.Expression.EvaluateAsync(context);
            }
        }

        return null;
    }

    private static async ValueTask<List<FluidValue>> PositionalListAsync(IReadOnlyList<FilterArgument> arguments, TemplateContext context)
    {
        var values = new List<FluidValue>();
        foreach (var argument in arguments)
        {
            values.Add(await argument.Expression.EvaluateAsync(context));
        }

        return values;
    }

    /// <summary>Liquid argument names are snake case; parameters are Pascal case (<c>start_value</c> → <c>StartValue</c>).</summary>
    public static string ToPascal(string name) =>
        string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
