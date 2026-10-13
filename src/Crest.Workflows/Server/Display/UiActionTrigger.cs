using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Contexts;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;

namespace Crest.Workflows.Display;

/// <summary>
/// The contract a workflow offers the UI (docs/blazor-display.md, rulings 2026-10-12, custom UI at
/// runtime): a workflow is runnable from a display action (a button, a menu item, a component's
/// event) only when it starts with this trigger, and the action's arguments must match the inputs
/// it declares. The declaration is one input per line, <c>name: type</c>, a trailing <c>?</c>
/// for an optional one; types are <c>string</c>, <c>number</c>, <c>boolean</c>, <c>object</c>,
/// <c>array</c>, <c>any</c>. Arguments are checked when a template is saved and again when the
/// action runs; a run with bad arguments ends on Invalid.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Starts the workflow from a UI action (a button, a menu item, a component event) with declared inputs.", DisplayName = "UI action")]
[FlowNode("Done", "Invalid", "Denied")]
public class UiActionTrigger : Trigger<IDictionary<string, object>>
{
    [Input(DisplayName = "Inputs", Description = "One input per line: name: type (string, number, boolean, object, array, any); a trailing ? marks it optional. Example: invoice: string", UIHint = InputUIHints.MultiLine)]
    public Input<string?> Inputs { get; set; } = null!;

    [Input(DisplayName = "Required permission", Description = "Optional. Crest permission the acting user must hold; otherwise the run ends on Denied.", UIHint = InputUIHints.DropDown, UIHandler = typeof(PermissionOptionsProvider))]
    public Input<string?> RequiredPermission { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var requiredPermission = RequiredPermission.GetOrDefault(context);
        if (!string.IsNullOrWhiteSpace(requiredPermission))
        {
            var authorizer = context.GetRequiredService<IWorkflowAuthorizer>();
            if (!await authorizer.AuthorizeAsync(requiredPermission, context.CancellationToken))
            {
                await context.CompleteActivityWithOutcomesAsync("Denied");
                return;
            }
        }

        var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload) ?? new Dictionary<string, object>();
        var contract = UiActionContract.Parse(Inputs.GetOrDefault(context));
        if (contract.Check(JsonSerializer.SerializeToNode(payload) as JsonObject ?? []).Count > 0)
        {
            await context.CompleteActivityWithOutcomesAsync("Invalid");
            return;
        }

        context.SetResult(payload);
        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    // The stored trigger carries the declaration, so the run endpoint and template validation read
    // the contract without loading the workflow.
    protected override ValueTask<IEnumerable<object>> GetTriggerPayloadsAsync(TriggerIndexingContext context) =>
        new([new UiActionTriggerPayload(Inputs.GetOrDefault(context.ExpressionExecutionContext) ?? string.Empty)]);
}

/// <summary>What the trigger index stores for a UI action trigger: its input declaration.</summary>
public sealed record UiActionTriggerPayload(string Inputs);

/// <summary>A UI action trigger's declared inputs, and the check of an action's arguments against them.</summary>
public sealed record UiActionContract(IReadOnlyList<UiActionInput> Inputs)
{
    private static readonly HashSet<string> s_types = new(StringComparer.OrdinalIgnoreCase) { "string", "number", "boolean", "object", "array", "any" };

    public static UiActionContract Parse(string? declaration)
    {
        var inputs = new List<UiActionInput>();
        foreach (var line in (declaration ?? string.Empty).Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            var name = (separator < 0 ? line : line[..separator]).Trim();
            var type = separator < 0 ? "any" : line[(separator + 1)..].Trim();
            var optional = type.EndsWith('?');
            type = type.TrimEnd('?').Trim();
            if (name.Length > 0)
            {
                inputs.Add(new UiActionInput(name, s_types.Contains(type) ? type.ToLowerInvariant() : "any", !optional));
            }
        }

        return new UiActionContract(inputs);
    }

    /// <summary>The names an action passes, checked by name only (values are bound at run time).</summary>
    public IReadOnlyList<string> CheckNames(IEnumerable<string> names) =>
        [.. CheckNamesByInput(names).SelectMany(pair => pair.Value)];

    /// <summary>The values an action sends, checked by name and type.</summary>
    public IReadOnlyList<string> Check(JsonObject values) =>
        [.. CheckByInput(values).SelectMany(pair => pair.Value)];

    /// <summary>
    /// <see cref="Check"/> by input name, so a form shows each error beside its input
    /// (docs/widgets.md, Validation).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CheckByInput(JsonObject values)
    {
        var errors = CheckNamesByInput(values.Select(pair => pair.Key));
        foreach (var input in Inputs)
        {
            var value = values.FirstOrDefault(pair => string.Equals(pair.Key, input.Name, StringComparison.OrdinalIgnoreCase)).Value;
            if (value is null)
            {
                continue;
            }

            var fits = input.Type switch
            {
                "string" => value.GetValueKind() == JsonValueKind.String,
                "number" => value.GetValueKind() == JsonValueKind.Number,
                "boolean" => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
                "object" => value is JsonObject,
                "array" => value is JsonArray,
                _ => true,
            };

            if (!fits)
            {
                Add(errors, input.Name, $"The input '{input.Name}' must be a {input.Type}.");
            }
        }

        return errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, List<string>> CheckNamesByInput(IEnumerable<string> names)
    {
        var given = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in Inputs.Where(input => input.Required && !given.Contains(input.Name)))
        {
            Add(errors, input.Name, $"The input '{input.Name}' is required.");
        }

        foreach (var name in given.Where(name => !Inputs.Any(input => string.Equals(input.Name, name, StringComparison.OrdinalIgnoreCase))))
        {
            Add(errors, name, $"'{name}' is not an input of this workflow.");
        }

        return errors;
    }

    private static void Add(Dictionary<string, List<string>> errors, string name, string error)
    {
        if (!errors.TryGetValue(name, out var list))
        {
            errors[name] = list = [];
        }

        list.Add(error);
    }
}

public sealed record UiActionInput(string Name, string Type, bool Required);
