using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// Runs an event's action list in order (§ 4a). Client actions change the page state; server
/// actions go through the shell's <see cref="IDisplayActionDispatcher"/>. A failing action stops
/// the list and is logged; the page keeps working.
/// </summary>
public sealed class DisplayActionRunner(IDisplayActionDispatcher? dispatcher, ILogger logger)
{
    /// <param name="actions">The list, run in order.</param>
    /// <param name="sourceNodeId">The node whose event fired.</param>
    /// <param name="payload">The event's payload (<c>event</c> in bindings).</param>
    /// <param name="position">Where the event happened, for an overlay it opens.</param>
    /// <param name="overlay">The overlay the source node is inside, which a close without a target closes.</param>
    public async Task RunAsync(
        IReadOnlyList<DisplayAction> actions,
        string sourceNodeId,
        JsonNode? payload,
        DisplayPoint? position,
        string? overlay,
        DisplayState state,
        CancellationToken cancellationToken = default)
    {
        foreach (var action in actions)
        {
            try
            {
                await RunAsync(action, sourceNodeId, payload, position, overlay, state, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "The {Kind} action of node '{Node}' failed; the rest of its list was skipped.", action.Kind, sourceNodeId);
                return;
            }
        }
    }

    private async Task RunAsync(DisplayAction action, string sourceNodeId, JsonNode? payload, DisplayPoint? position, string? overlay, DisplayState state, CancellationToken cancellationToken)
    {
        switch (action.Kind)
        {
            case DisplayActionKind.Set when action.Target is { Length: > 0 } variable:
                state.SetVariable(variable, action.Value is null ? payload : state.Read(action.Value, payload));
                break;

            case DisplayActionKind.Open when action.Target is { Length: > 0 } target:
                state.Open(target, position, payload);
                break;

            case DisplayActionKind.Close when (action.Target ?? overlay) is { Length: > 0 } target:
                state.Close(target);
                break;

            case DisplayActionKind.Toggle when action.Target is { Length: > 0 } target:
                if (state.IsOpen(target))
                {
                    state.Close(target);
                }
                else
                {
                    state.Open(target, position, payload);
                }

                break;

            case DisplayActionKind.Workflow when action.Workflow is { Length: > 0 } workflow:
                var input = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                foreach (var (name, argument) in action.Arguments ?? [])
                {
                    input[name] = state.Read(argument, payload);
                }

                var result = await Dispatcher(action).RunWorkflowAsync(workflow, input, cancellationToken);
                if (action.Target is { Length: > 0 } into)
                {
                    state.SetVariable(into, result);
                }

                break;

            case DisplayActionKind.Navigate when action.Value is { } value:
                if (state.Read(value, payload) is JsonValue url && url.TryGetValue<string>(out var href) && href.Length > 0)
                {
                    Dispatcher(action).Navigate(href);
                }

                break;

            case DisplayActionKind.Submit:
                await Dispatcher(action).SubmitAsync(sourceNodeId, payload, cancellationToken);
                break;

            default:
                logger.LogWarning("The {Kind} action of node '{Node}' is incomplete and was skipped.", action.Kind, sourceNodeId);
                break;
        }
    }

    private IDisplayActionDispatcher Dispatcher(DisplayAction action) =>
        dispatcher ?? throw new InvalidOperationException($"No action dispatcher is registered in this runtime; the {action.Kind} action needs the server.");
}
