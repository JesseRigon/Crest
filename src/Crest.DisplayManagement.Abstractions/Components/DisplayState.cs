using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The page state of one rendered tree (§ 4a): the latest value of every node's outputs, the
/// page variables and the open overlays. The renderer creates one per tree and cascades it;
/// a change re-renders the tree, so bound parameters read the new values. Values are held as
/// JSON so a binding's path reads into them the same way on every side. Nothing here is
/// trusted: it is the viewer's own input.
/// </summary>
public sealed class DisplayState
{
    private readonly Dictionary<string, JsonNode?> _outputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JsonNode?> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (DisplayPoint? Position, JsonNode? Context)> _open = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonNode?> _slots = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DisplayBinding> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> _errors = new(StringComparer.Ordinal);

    /// <summary>Raised after any change, once per change.</summary>
    public event Action? Changed;

    public IReadOnlyDictionary<string, JsonNode?> Variables => _variables;

    public static string OutputKey(string nodeId, string output) => nodeId + "." + output;

    public JsonNode? GetOutput(string nodeId, string output) =>
        _outputs.TryGetValue(OutputKey(nodeId, output), out var value) ? value?.DeepClone()
        : _aliases.TryGetValue(OutputKey(nodeId, output), out var alias) ? Read(alias)
        : null;

    /// <summary>Makes <c>{nodeId}.{output}</c> read <paramref name="source"/> (a composite re-publishing an inner node's output).</summary>
    public void RegisterAlias(string nodeId, string output, DisplayBinding source) => _aliases[OutputKey(nodeId, output)] = source;

    public void SetOutput(string nodeId, string output, object? value)
    {
        _outputs[OutputKey(nodeId, output)] = ToJson(value);
        Changed?.Invoke();
    }

    public JsonNode? GetVariable(string name) => _variables.TryGetValue(name, out var value) ? value?.DeepClone() : null;

    public void SetVariable(string name, JsonNode? value)
    {
        _variables[name] = value?.DeepClone();
        Changed?.Invoke();
    }

    /// <summary>
    /// The errors the last server action of <paramref name="nodeId"/>'s events returned, by input
    /// name (an empty name for errors that belong to no input); empty when it succeeded.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetErrors(string nodeId) =>
        _errors.TryGetValue(nodeId, out var errors) ? errors : s_noErrors;

    public void SetErrors(string nodeId, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors)
    {
        if (errors is not { Count: > 0 })
        {
            if (_errors.Remove(nodeId))
            {
                Changed?.Invoke();
            }

            return;
        }

        _errors[nodeId] = errors;
        Changed?.Invoke();
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> s_noErrors = new Dictionary<string, IReadOnlyList<string>>();

    public bool IsOpen(string nodeId) => _open.ContainsKey(nodeId);

    public (DisplayPoint? Position, JsonNode? Context) OpenState(string nodeId) => _open.TryGetValue(nodeId, out var state) ? state : default;

    public void Open(string nodeId, DisplayPoint? position = null, JsonNode? context = null)
    {
        _open[nodeId] = (position, context?.DeepClone());
        Changed?.Invoke();
    }

    public void Close(string nodeId)
    {
        if (_open.Remove(nodeId))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>The key a slot's result is cached under: the query and its current parameters.</summary>
    public string SlotKey(DisplaySlot slot, out Dictionary<string, JsonNode?> parameters)
    {
        parameters = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (name, binding) in slot.Parameters ?? [])
        {
            parameters[name] = Read(binding);
        }

        var ordered = new JsonObject();
        foreach (var (name, value) in parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ordered[name] = value?.DeepClone();
        }

        return slot.Query + "?" + ordered.ToJsonString();
    }

    /// <summary>
    /// The slot's value for its current parameters: cached, or loading (null until it arrives;
    /// a change is raised when it does). One load per key at a time.
    /// </summary>
    public bool TryGetSlot(DisplaySlot slot, IDisplayDataSource? source, out JsonNode? value)
    {
        var key = SlotKey(slot, out var parameters);
        if (_slots.TryGetValue(key, out var result))
        {
            value = slot.Path is { Length: > 0 } path ? Navigate(result, path) : result?.DeepClone();
            return true;
        }

        value = null;
        if (source is not null && _loading.Add(key))
        {
            _ = LoadAsync(key, slot, parameters, source);
        }

        return false;
    }

    private async Task LoadAsync(string key, DisplaySlot slot, Dictionary<string, JsonNode?> parameters, IDisplayDataSource source)
    {
        JsonNode? result = null;
        try
        {
            result = await source.QueryAsync(slot.Query, parameters);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A failed read shows as an empty slot; the next change of its parameters retries.
        }
        finally
        {
            _loading.Remove(key);
        }

        _slots[key] = result;
        Changed?.Invoke();
    }

    /// <summary>The value <paramref name="binding"/> reads now; <paramref name="eventPayload"/> is
    /// the payload of the event being handled, if any. Missing values fall back to the binding's
    /// literal.</summary>
    public JsonNode? Read(DisplayBinding binding, JsonNode? eventPayload = null)
    {
        JsonNode? source = binding switch
        {
            { Event: true } => eventPayload?.DeepClone(),
            { Node: { } node, Output: { } output } => GetOutput(node, output),
            { Variable: { } variable } => GetVariable(variable),
            _ => binding.Value?.DeepClone(),
        };

        if (!binding.IsLiteral && binding.Path is { Length: > 0 } path)
        {
            source = Navigate(source, path);
        }

        return source ?? (binding.IsLiteral ? null : binding.Value?.DeepClone());
    }

    /// <summary>Converts a CLR value to the JSON form the state holds (the renderer's options).</summary>
    public static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        _ => JsonSerializer.SerializeToNode(value, value.GetType(), ComponentParameters.JsonOptions),
    };

    /// <summary>Reads <paramref name="path"/> (<c>a.b[0].c</c>, members case-insensitive) into <paramref name="node"/>.</summary>
    public static JsonNode? Navigate(JsonNode? node, string path)
    {
        foreach (var segment in Segments(path))
        {
            node = (node, segment) switch
            {
                (JsonArray array, { Index: { } index }) => index >= 0 && index < array.Count ? array[index] : null,
                (JsonObject obj, { Member: { } member }) => Member(obj, member),
                _ => null,
            };

            if (node is null)
            {
                return null;
            }
        }

        return node?.DeepClone();
    }

    private static JsonNode? Member(JsonObject obj, string member)
    {
        if (obj.TryGetPropertyValue(member, out var exact))
        {
            return exact;
        }

        foreach (var (name, value) in obj)
        {
            if (string.Equals(name, member, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<(string? Member, int? Index)> Segments(string path)
    {
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bracket = part.IndexOf('[');
            var member = bracket < 0 ? part : part[..bracket];
            if (member.Length > 0)
            {
                yield return (member, null);
            }

            while (bracket >= 0)
            {
                var close = part.IndexOf(']', bracket);
                if (close < 0)
                {
                    yield break;
                }

                yield return (null, int.TryParse(part.AsSpan(bracket + 1, close - bracket - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : -1);
                bracket = part.IndexOf('[', close);
            }
        }
    }
}
