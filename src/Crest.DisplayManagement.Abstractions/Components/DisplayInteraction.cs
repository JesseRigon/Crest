using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// Where a value comes from at run time (§ 4a): another node's output, a page variable, the
/// payload of the event being handled, or a literal. A binding with a <see cref="Path"/> reads
/// into the value (<c>id</c>, <c>item.id</c>, <c>items[0].name</c>). Nothing a binding reads is
/// trusted: it is the viewer's own input, re-checked by every query and workflow it reaches.
/// </summary>
public sealed class DisplayBinding
{
    /// <summary>The instance id of the node whose output is read.</summary>
    public string? Node { get; init; }

    /// <summary>The output's name on that node (a <see cref="ParameterKind.Output"/> parameter).</summary>
    public string? Output { get; init; }

    /// <summary>A page variable, set by a <see cref="DisplayActionKind.Set"/> action.</summary>
    public string? Variable { get; init; }

    /// <summary>The payload of the event an action handles.</summary>
    public bool Event { get; init; }

    /// <summary>A literal; also the default when the bound value is not there yet.</summary>
    public JsonNode? Value { get; init; }

    /// <summary>A path into the value: dot-separated members, <c>[n]</c> for an index.</summary>
    public string? Path { get; init; }

    public static DisplayBinding Literal(JsonNode? value) => new() { Value = value };

    public static DisplayBinding FromOutput(string node, string output, string? path = null) => new() { Node = node, Output = output, Path = path };

    public static DisplayBinding FromVariable(string variable, string? path = null) => new() { Variable = variable, Path = path };

    public static DisplayBinding FromEvent(string? path = null) => new() { Event = true, Path = path };

    [JsonIgnore]
    public bool IsLiteral => Node is null && Variable is null && !Event;
}

/// <summary>
/// A data slot (decision 4): a parameter fed by a query. Its parameters are bindings, so a slot
/// whose parameter reads another node's output re-runs when that output changes (the invoice
/// details re-query when the list's selection changes). The server resolves a slot whose
/// parameters are all literal while it builds the tree; the browser re-runs it through
/// <see cref="IDisplayDataSource"/> when a bound parameter changes. A slot never binds a write.
/// </summary>
public sealed class DisplaySlot
{
    public required string Query { get; init; }

    public Dictionary<string, DisplayBinding>? Parameters { get; init; }

    /// <summary>A path into the result (<c>items</c>, <c>items[0]</c>, <c>total</c>); the whole result when absent.</summary>
    public string? Path { get; init; }

    /// <summary>True when every parameter is a literal: the value is fixed when the tree is built.</summary>
    [JsonIgnore]
    public bool IsStatic => Parameters is null || Parameters.Values.All(parameter => parameter.IsLiteral);
}

/// <summary>Runs a slot's query for the viewer (§ 4a, decision 4): the query system applies the caller's scope.</summary>
public interface IDisplayDataSource
{
    /// <summary>The query's result as JSON (<c>{ items, columns, total, nextPageToken }</c>); null when refused or missing.</summary>
    Task<JsonNode?> QueryAsync(string query, IReadOnlyDictionary<string, JsonNode?> parameters, CancellationToken cancellationToken = default);
}

/// <summary>The actions an event may run (decision 5 and § 4a).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DisplayActionKind>))]
public enum DisplayActionKind
{
    /// <summary>Sets a page variable (<see cref="DisplayAction.Target"/>) to <see cref="DisplayAction.Value"/>.</summary>
    Set,

    /// <summary>Opens an overlay node (<see cref="DisplayAction.Target"/>), at the event's position when it has one.</summary>
    Open,

    /// <summary>Closes an overlay node; with no target, the overlay the event came from.</summary>
    Close,

    /// <summary>Opens a closed overlay node, closes an open one.</summary>
    Toggle,

    /// <summary>Runs a workflow on the server with <see cref="DisplayAction.Arguments"/>; its output may be stored in a variable.</summary>
    Workflow,

    /// <summary>Navigates to <see cref="DisplayAction.Value"/>.</summary>
    Navigate,

    /// <summary>Submits the current item.</summary>
    Submit,
}

/// <summary>
/// One action of an event's list (§ 4a), run in order. Client actions (set, open, close, toggle)
/// only change what the viewer sees; the server actions (workflow, navigate, submit) reach data
/// only through the server, which checks them.
/// </summary>
public sealed class DisplayAction
{
    public required DisplayActionKind Kind { get; init; }

    /// <summary>The variable a set (or a workflow's result) writes, or the overlay node an open, close or toggle acts on.</summary>
    public string? Target { get; init; }

    /// <summary>The value of a set, the URL of a navigate.</summary>
    public DisplayBinding? Value { get; init; }

    /// <summary>The workflow definition a workflow action runs.</summary>
    public string? Workflow { get; init; }

    /// <summary>The workflow's input, by name.</summary>
    public Dictionary<string, DisplayBinding>? Arguments { get; init; }

    public bool IsClientOnly => Kind is DisplayActionKind.Set or DisplayActionKind.Open or DisplayActionKind.Close or DisplayActionKind.Toggle;
}

/// <summary>A position on the page an overlay opens at (a right-click's client coordinates).</summary>
public sealed record DisplayPoint(double X, double Y);

/// <summary>Cascaded to an open overlay node: where it opened and how it closes itself.</summary>
public sealed class DisplayOverlay
{
    public required string NodeId { get; init; }

    public DisplayPoint? Position { get; init; }

    /// <summary>The payload of the event that opened it (the right-clicked row).</summary>
    public JsonNode? Context { get; init; }

    public required Func<Task> CloseAsync { get; init; }
}

/// <summary>
/// An event payload that carries a position (a mouse event's client coordinates), so an
/// <see cref="DisplayActionKind.Open"/> places the overlay there. Components with a positional
/// event raise a payload implementing it.
/// </summary>
public interface IDisplayPositioned
{
    DisplayPoint Position { get; }
}

/// <summary>
/// Runs the server actions of a display tree in the browser or on the server (§ 4a): the shell
/// registers one; without it the renderer reports server actions and skips them.
/// </summary>
public interface IDisplayActionDispatcher
{
    /// <summary>Runs the workflow; returns its output (null when it has none).</summary>
    Task<JsonNode?> RunWorkflowAsync(string workflow, IReadOnlyDictionary<string, JsonNode?> input, CancellationToken cancellationToken = default);

    void Navigate(string url);

    Task SubmitAsync(string nodeId, JsonNode? values, CancellationToken cancellationToken = default);
}
