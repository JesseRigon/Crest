using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Grove.Display.Management.Components;

/// <summary>
/// The predicate grammar over bindings (docs/widgets.md, ruling 2026-10-13: comparisons in
/// bindings): a fixed operator list, <c>and</c> / <c>or</c> / <c>not</c>, and nothing else. The
/// template parser stores a predicate as data on a <see cref="DisplayBinding"/>, the validator
/// checks it here, and <see cref="DisplayState"/> evaluates it here, in the browser, whenever a
/// source changes. Field conditions (<see cref="FieldConditions"/>) are evaluated here too: one
/// evaluator for display bindings, field visibility and a type's field rules. Text compares
/// ordinally; numbers as numbers; a list equals a value it holds.
/// </summary>
public static class DisplayPredicates
{
    public const string Equal = "==";
    public const string NotEqual = "!=";
    public const string Empty = "empty";
    public const string NotEmpty = "not empty";
    public const string Contains = "contains";
    public const string StartsWith = "starts_with";
    public const string EndsWith = "ends_with";
    public const string Greater = ">";
    public const string Less = "<";

    public const string And = "and";
    public const string Or = "or";
    public const string Not = "not";

    public static IReadOnlyList<string> Operators { get; } = [Equal, NotEqual, Empty, NotEmpty, Contains, StartsWith, EndsWith, Greater, Less];

    public static IReadOnlyList<string> Combinators { get; } = [And, Or, Not];

    /// <summary>Whether the operator takes an operand (<c>empty</c> and <c>not empty</c> do not).</summary>
    public static bool TakesOperand(string @operator) => @operator is not (Empty or NotEmpty);

    /// <summary>
    /// The value <paramref name="binding"/> reads, its comparisons and combinations evaluated
    /// here; <paramref name="readSource"/> reads a plain source (a node's output, a variable, the
    /// event, a field, a literal).
    /// </summary>
    public static JsonNode? Read(DisplayBinding binding, Func<DisplayBinding, JsonNode?> readSource)
    {
        if (binding.Combinator is { } combinator)
        {
            var terms = binding.Terms ?? [];
            return JsonValue.Create(combinator switch
            {
                And => terms.All(term => IsTrue(Read(term, readSource))),
                Or => terms.Any(term => IsTrue(Read(term, readSource))),
                Not => terms.Count > 0 && !IsTrue(Read(terms[0], readSource)),
                _ => false,
            });
        }

        if (binding.Operator is { } @operator)
        {
            var left = readSource(binding);
            var right = binding.Operand is { } operand ? Read(operand, readSource) : null;
            return JsonValue.Create(Evaluate(@operator, left, right));
        }

        return readSource(binding);
    }

    public static bool Evaluate(string @operator, JsonNode? left, JsonNode? right) => @operator switch
    {
        Equal => Equals(left, right),
        NotEqual => !Equals(left, right),
        Empty => IsEmpty(left),
        NotEmpty => !IsEmpty(left),
        Contains => left is JsonArray items ? items.Any(item => Equals(item, right)) : Text(left).Contains(Text(right), StringComparison.Ordinal),
        StartsWith => Text(left).StartsWith(Text(right), StringComparison.Ordinal),
        EndsWith => Text(left).EndsWith(Text(right), StringComparison.Ordinal),
        Greater => Order(left, right) > 0,
        Less => Order(left, right) < 0,
        _ => false,
    };

    /// <summary>A value as a condition: booleans as they are, null and empty text false, numbers non-zero, anything else present.</summary>
    public static bool IsTrue(JsonNode? value) => value switch
    {
        null => false,
        JsonValue scalar when scalar.TryGetValue<bool>(out var flag) => flag,
        JsonValue scalar when scalar.GetValueKind() == JsonValueKind.Number => !decimal.TryParse(scalar.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number != 0,
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => text.Length > 0,
        _ => true,
    };

    /// <summary>The node ids a binding reads, its operand's and its terms' included.</summary>
    public static IEnumerable<string> Sources(DisplayBinding binding)
    {
        if (binding.Node is { } node)
        {
            yield return node;
        }

        if (binding.Operand is { } operand)
        {
            foreach (var source in Sources(operand))
            {
                yield return source;
            }
        }

        foreach (var term in binding.Terms ?? [])
        {
            foreach (var source in Sources(term))
            {
                yield return source;
            }
        }
    }

    /// <summary>The binding with every node id it reads passed through <paramref name="map"/> (a tree re-scoped under an instance).</summary>
    public static DisplayBinding MapNodes(DisplayBinding binding, Func<string, string> map) => new()
    {
        Node = binding.Node is { } node ? map(node) : null,
        Output = binding.Output,
        Variable = binding.Variable,
        Event = binding.Event,
        Field = binding.Field,
        Value = binding.Value?.DeepClone(),
        Path = binding.Path,
        Operator = binding.Operator,
        Operand = binding.Operand is { } operand ? MapNodes(operand, map) : null,
        Combinator = binding.Combinator,
        Terms = binding.Terms?.Select(term => MapNodes(term, map)).ToList(),
    };

    private static bool Equals(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            return IsEmpty(left) && IsEmpty(right);
        }

        // A list (a field holding several values) equals each value it holds.
        if (left is JsonArray items && right is JsonValue)
        {
            return items.Any(item => Equals(item, right));
        }

        if (left is JsonValue a && right is JsonValue b)
        {
            if (Number(a, b) is var (x, y))
            {
                return x == y;
            }

            if (a.TryGetValue<bool>(out var p) && b.TryGetValue<bool>(out var q))
            {
                return p == q;
            }

            return string.Equals(Text(a), Text(b), StringComparison.Ordinal);
        }

        return JsonNode.DeepEquals(left, right);
    }

    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => text.Length == 0,
        JsonValue scalar => scalar.GetValueKind() == JsonValueKind.Null,
        JsonArray items => items.Count == 0,
        JsonObject members => members.Count == 0,
        _ => false,
    };

    private static int Order(JsonNode? left, JsonNode? right)
    {
        if (left is JsonValue a && right is JsonValue b && Number(a, b) is var (x, y))
        {
            return x.CompareTo(y);
        }

        return string.CompareOrdinal(Text(left), Text(right));
    }

    // Numbers compare as numbers; text that reads as a number (an input's value) does too when the
    // other side is a number.
    private static (decimal, decimal)? Number(JsonValue a, JsonValue b)
    {
        var x = AsNumber(a, out var aIsNumber);
        var y = AsNumber(b, out var bIsNumber);
        return x is { } left && y is { } right && (aIsNumber || bIsNumber) ? (left, right) : null;
    }

    private static decimal? AsNumber(JsonValue value, out bool isNumber)
    {
        isNumber = value.GetValueKind() == JsonValueKind.Number;
        if (isNumber)
        {
            // A value created from an int answers TryGetValue<decimal> with false: read its JSON.
            return decimal.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        return value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string Text(JsonNode? value) => value switch
    {
        null => string.Empty,
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => text,
        JsonValue scalar when scalar.GetValueKind() == JsonValueKind.Number => scalar.ToJsonString(),
        JsonValue scalar when scalar.TryGetValue<bool>(out var flag) => flag ? "true" : "false",
        _ => value.ToJsonString(),
    };
}
